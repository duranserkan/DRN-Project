using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class EntityAppIdResolver(Compilation compilation)
{
    private readonly INamedTypeSymbol? _attribute = compilation.GetTypeByMetadataName(EntityAnalyzerHelper.EntityTypeAttributeMetadataName + "`1");
    private readonly IPropertySymbol? _appId = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.IAppId")
        ?.GetMembers("AppId").OfType<IPropertySymbol>().FirstOrDefault();

    internal byte? Resolve(INamedTypeSymbol attribute, CancellationToken cancellation)
    {
        for (var current = attribute; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, _attribute) &&
                current.TypeArguments is [INamedTypeSymbol app])
                return ReadPartition(app, cancellation);
        }
        return null;
    }

    private byte? ReadPartition(INamedTypeSymbol app, CancellationToken cancellation)
    {
        byte? constant = null;
        foreach (var member in app.GetMembers())
        {
            cancellation.ThrowIfCancellationRequested();
            if (member is not IFieldSymbol
                {
                    Name: "AppId" or "Value", DeclaredAccessibility: Accessibility.Public, HasConstantValue: true
                } field ||
                !EntityAnalyzerHelper.TryExtractByte(field.ConstantValue, out var value)) continue;
            if (constant.HasValue && constant != value) return null;
            constant = value;
        }
        // Consumers of compiled partitions can verify only their published constant.
        if (constant is null || app.DeclaringSyntaxReferences.IsEmpty) return constant;
        if (_appId is null || app.FindImplementationForInterfaceMember(_appId) is not IPropertySymbol property) return null;
        if (ReadGetter(property, compilation, cancellation) != constant) return null;

        return constant;
    }

    private static byte? ReadGetter(IPropertySymbol property, Compilation owner, CancellationToken cancellation)
    {
        var visited = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        while (visited.Add(property.OriginalDefinition))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!property.IsStatic || !CanRepresentByte(property.Type) || property.SetMethod is not null || property.DeclaringSyntaxReferences is not [var reference] ||
                reference.GetSyntax(cancellation) is not PropertyDeclarationSyntax syntax) return null;
            if (!owner.ContainsSyntaxTree(reference.SyntaxTree))
            {
                if (FindOwningCompilation(owner, property.ContainingAssembly, reference.SyntaxTree, cancellation) is not { } producer)
                    return null;
                owner = producer;
            }
            var expression = GetterExpression(syntax, property);
            if (expression is null) return null;
            var model = owner.GetSemanticModel(reference.SyntaxTree);
            if (model.GetConstantValue(expression, cancellation) is { HasValue: true } constant &&
                EntityAnalyzerHelper.TryExtractByte(constant.Value, out var value)) return value;
            if (ForwardedProperty(model.GetOperation(expression, cancellation)) is not { } next) return null;
            property = next;
        }
        return null;
    }

    private static Compilation? FindOwningCompilation(Compilation root, IAssemblySymbol targetAssembly, SyntaxTree tree,
        CancellationToken cancellation)
    {
        if (root.GetMetadataReference(targetAssembly) is CompilationReference direct &&
            direct.Compilation.ContainsSyntaxTree(tree))
            return direct.Compilation;

        var pending = new Stack<Compilation>();
        var visited = new HashSet<Compilation>();
        pending.Push(root);
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            foreach (var reference in current.References)
            {
                if (reference is not CompilationReference nested) continue;
                if (nested.Compilation.ContainsSyntaxTree(tree)) return nested.Compilation;
                pending.Push(nested.Compilation);
            }
        }
        return null;
    }

    private static IPropertySymbol? ForwardedProperty(IOperation? value)
    {
        while (value is IConversionOperation { OperatorMethod: null } conversion && CanRepresentByte(conversion.Type))
            value = conversion.Operand;
        return (value as IPropertyReferenceOperation)?.Property;
    }

    private static bool CanRepresentByte(ITypeSymbol? type) => type?.SpecialType is SpecialType.System_Byte or
        SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
        SpecialType.System_Int64 or SpecialType.System_UInt64;

    private static ExpressionSyntax? GetterExpression(PropertyDeclarationSyntax property, IPropertySymbol symbol)
    {
        if (property.ExpressionBody is { } arrow) return arrow.Expression;
        if (property.AccessorList?.Accessors is not [var getter]) return null;
        if (getter.ExpressionBody is { } body) return body.Expression;
        if (getter.Body is null)
            return symbol.ContainingType.StaticConstructors.Any(constructor => !constructor.IsImplicitlyDeclared)
                ? null : property.Initializer?.Value;
        var statements = getter.Body.Statements;
        if (statements.LastOrDefault() is not ReturnStatementSyntax result) return null;
        // A conditional return can select another partition. Only local constants may precede the return.
        for (var i = 0; i < statements.Count - 1; i++)
            if (statements[i] is not LocalDeclarationStatementSyntax local || !local.Modifiers.Any(SyntaxKind.ConstKeyword)) return null;
        return result.Expression;
    }
}
