using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class EntityAppIdResolver(Compilation compilation, IAssemblySymbol? framework)
{
    private readonly INamedTypeSymbol? _attribute = framework?.GetTypeByMetadataName(EntityAnalyzerHelper.EntityTypeAttributeMetadataName + "`1");
    private readonly IPropertySymbol? _appId = framework?.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.IAppId")
        ?.GetMembers("AppId").OfType<IPropertySymbol>().FirstOrDefault();

    internal bool ImplementsAppId(INamedTypeSymbol type) =>
        _appId is not null && type.AllInterfaces.Contains(_appId.ContainingType, SymbolEqualityComparer.Default);

    internal byte? Resolve(INamedTypeSymbol attribute, CancellationToken cancellation) =>
        PartitionType(attribute, cancellation) is { } app ? ReadPartition(app, cancellation) : null;

    internal INamedTypeSymbol? PartitionType(INamedTypeSymbol attribute, CancellationToken cancellation)
    {
        for (var current = attribute; current != null; current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, _attribute) &&
                current.TypeArguments is [INamedTypeSymbol app])
                return app;
        }
        return null;
    }

    internal byte? ReadPartition(INamedTypeSymbol app, CancellationToken cancellation)
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
        Stack<IConversionOperation>? conversions = null;
        while (visited.Add(property.OriginalDefinition))
        {
            cancellation.ThrowIfCancellationRequested();
            if (GetPropertySyntax(property, cancellation) is not { } syntax ||
                GetterExpression(syntax, property) is not { } expression) return null;
            if (!owner.ContainsSyntaxTree(syntax.SyntaxTree))
            {
                if (FindOwningCompilation(owner, property.ContainingAssembly, syntax.SyntaxTree, cancellation) is not { } producer)
                    return null;
                owner = producer;
            }
            var model = owner.GetSemanticModel(syntax.SyntaxTree);
            if (model.GetConstantValue(expression, cancellation) is { HasValue: true, Value: { } constant })
                return ConvertPartition(constant, conversions, cancellation);
            if (syntax.Initializer is not null ||
                ReferencedProperty(model, expression, ref conversions, cancellation) is not { } next) return null;
            property = next;
        }
        return null;
    }

    private static PropertyDeclarationSyntax? GetPropertySyntax(IPropertySymbol property, CancellationToken cancellation) =>
        property.IsStatic && IntegralConstant.Supports(property.Type) && property.SetMethod is null &&
        property.DeclaringSyntaxReferences is [var reference] ? reference.GetSyntax(cancellation) as PropertyDeclarationSyntax : null;

    private static byte? ConvertPartition(object constant, Stack<IConversionOperation>? conversions, CancellationToken cancellation)
    {
        object value = constant is char character ? (ushort)character : constant;
        while (conversions is not null && conversions.TryPop(out var cast))
        {
            cancellation.ThrowIfCancellationRequested();
            if (IntegralConstant.ConvertValue(value, cast.Type!.SpecialType, cast.IsChecked) is not { } converted) return null;
            value = converted;
        }
        return EntityAnalyzerHelper.TryExtractByte(value, out var partition) ? partition : null;
    }

    private static IPropertySymbol? ReferencedProperty(SemanticModel model, ExpressionSyntax expression,
        ref Stack<IConversionOperation>? conversions, CancellationToken cancellation)
    {
        while (expression is CheckedExpressionSyntax or ParenthesizedExpressionSyntax)
            expression = expression is CheckedExpressionSyntax scope ? scope.Expression : ((ParenthesizedExpressionSyntax)expression).Expression;
        var operation = model.GetOperation(expression, cancellation);
        while (operation is IConversionOperation { OperatorMethod: null } conversion && IntegralConstant.Supports(conversion.Type))
        {
            (conversions ??= new()).Push(conversion);
            operation = conversion.Operand;
        }
        return (operation as IPropertyReferenceOperation)?.Property;
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
