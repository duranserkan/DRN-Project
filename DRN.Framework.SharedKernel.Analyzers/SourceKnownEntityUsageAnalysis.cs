using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class SourceKnownEntityUsageAnalysis
{
    private const string Domain = "DRN.Framework.SharedKernel.Domain.";
    private const string ValidateMethodName = "Validate";
    private readonly INamedTypeSymbol _entity;
    private readonly INamedTypeSymbol? _identity;
    private readonly INamedTypeSymbol? _format;
    private readonly INamedTypeSymbol? _repositoryInterface;
    private readonly INamedTypeSymbol? _repositoryBase;
    private readonly HashSet<object> _formats;
    private readonly HashSet<ISymbol> _metadataMethods = new(SymbolEqualityComparer.Default);

    private SourceKnownEntityUsageAnalysis(Compilation compilation, INamedTypeSymbol entity)
    {
        _entity = entity;
        _repositoryInterface = compilation.GetTypeByMetadataName(Domain + "Repository.ISourceKnownRepository`1");
        _repositoryBase = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Domain.SourceKnownRepository`2");
        _identity = compilation.GetTypeByMetadataName(Domain + "EntityTypeId");
        _format = compilation.GetTypeByMetadataName(Domain + "SourceKnownEntityIdFormat");
        _formats = _format?.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue && field.ConstantValue != null)
            .Select(field => field.ConstantValue!).ToHashSet() ?? [];

        AddMethods(compilation, Domain + "SourceKnownEntity", "GetEntityType", "GetEntityTypeId", "GetAppId", "GetEntityId");
        AddMethods(compilation, Domain + "EntityTypeRegistry", "GetEntityTypeId");
        AddMethods(compilation, Domain + "SourceKnownEntityId", ValidateMethodName, "HasSameEntityType", "HasSameEntityTypeId");
        AddMethods(compilation, Domain + "ISourceKnownEntityIdOperations", ValidateMethodName);
        AddMethods(compilation, Domain + "Repository.ISourceKnownRepository`1", "GetEntityId", "GetEntityIds", "GetEntityIdsAsEnumerable");
        AddMethods(compilation, "DRN.Framework.EntityFramework.Domain.SourceKnownRepository`2", "GetEntityId", "GetEntityIds", "GetEntityIdsAsEnumerable");
        AddMethods(compilation, "DRN.Framework.Utils.Ids.ISourceKnownEntityIdUtils", ValidateMethodName, "Generate", "GeneratePlain", "GenerateSecure");
        AddMethods(compilation, "DRN.Framework.Utils.Ids.SourceKnownEntityIdUtils", ValidateMethodName, "Generate", "GeneratePlain", "GenerateSecure");
        AddMethods(compilation, "DRN.Framework.Utils.Ids.ISourceKnownIdUtils", "Next");
        AddMethods(compilation, "DRN.Framework.Utils.Ids.SourceKnownIdUtils", "Next");
    }

    internal static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol entity)
    {
        var analysis = new SourceKnownEntityUsageAnalysis(context.Compilation, entity);
        context.RegisterOperationAction(analysis.AnalyzeArgument, OperationKind.Argument);
        context.RegisterOperationAction(analysis.AnalyzeAssignment, OperationKind.SimpleAssignment);
        context.RegisterOperationAction(analysis.AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(analysis.AnalyzeMethodReference, OperationKind.MethodReference);
        context.RegisterSymbolAction(analysis.AnalyzeType, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(analysis.AnalyzeRepositoryType, SyntaxKind.GenericName, SyntaxKind.IdentifierName);
    }

    private void AnalyzeRepositoryType(SyntaxNodeAnalysisContext context)
    {
        if (_repositoryInterface == null && _repositoryBase == null) return;
        if (context.Node.Parent is NameEqualsSyntax) return; // Alias declaration name; its target is checked separately.
        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;
        if (symbol is IAliasSymbol alias) symbol = alias.Target;
        if (symbol is not INamedTypeSymbol type || type.IsUnboundGenericType) return;

        var reported = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = type; current != null; current = current.BaseType)
            CheckRepositoryBinding(context, type, current, reported);
        foreach (var implemented in type.AllInterfaces)
            CheckRepositoryBinding(context, type, implemented, reported);
    }

    private void CheckRepositoryBinding(SyntaxNodeAnalysisContext context, INamedTypeSymbol usedType,
        INamedTypeSymbol binding, HashSet<ISymbol> reported)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var index = -1;
        if (SymbolEqualityComparer.Default.Equals(binding.OriginalDefinition, _repositoryInterface)) index = 0;
        else if (SymbolEqualityComparer.Default.Equals(binding.OriginalDefinition, _repositoryBase)) index = 1;
        if (index < 0 || binding.TypeArguments[index] is not INamedTypeSymbol { IsAbstract: true } entity ||
            !IsEntity(entity) || !reported.Add(entity)) return;

        // Point at the entity argument when it is written directly on a framework repository.
        var location = context.Node is GenericNameSyntax name &&
                       SymbolEqualityComparer.Default.Equals(usedType, binding)
            ? name.TypeArgumentList.Arguments[index].GetLocation()
            : context.Node.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.AbstractEntityMetadataArgument,
            location, entity.ToDisplayString(), usedType.ToDisplayString()));
    }

    private void AddMethods(Compilation compilation, string metadataName, params string[] names)
    {
        var type = compilation.GetTypeByMetadataName(metadataName);
        if (type == null) return;
        foreach (var name in names)
        {
            foreach (var method in type.GetMembers(name).OfType<IMethodSymbol>())
                _metadataMethods.Add(method.OriginalDefinition);
        }
    }

    private void AnalyzeArgument(OperationAnalysisContext context)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (argument.Parameter == null) return;
        CheckConstant(context, argument.Value, argument.Parameter.Type,
            argument.Parameter.Name == "AppId" &&
            argument.Parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor &&
            SymbolEqualityComparer.Default.Equals(constructor.ContainingType, _identity));
    }

    private void AnalyzeAssignment(OperationAnalysisContext context)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        var isAppId = assignment.Target is IPropertyReferenceOperation property &&
                      property.Property.Name == "AppId" &&
                      SymbolEqualityComparer.Default.Equals(property.Property.ContainingType, _identity);
        CheckConstant(context, assignment.Value, assignment.Target.Type, isAppId);
    }

    private void CheckConstant(OperationAnalysisContext context, IOperation value, ITypeSymbol? type, bool isAppId)
    {
        if (!value.ConstantValue.HasValue || value.ConstantValue.Value is not { } constant) return;
        if (isAppId && constant is byte appId && appId > EntityAnalyzerHelper.MaxAppId)
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidAppIdArgument, value.Syntax.GetLocation(), appId));
        else if (_format != null && SymbolEqualityComparer.Default.Equals(type, _format) && !_formats.Contains(constant))
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidEntityIdFormatArgument, value.Syntax.GetLocation(), constant));
    }

    private void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || !EntityAnalyzerHelper.DerivesFrom(type, _entity)) return;

        if (!type.IsAbstract && !EntityAnalyzerHelper.IsEffectivelyPrivate(type) && HasGenericContainerOrParameters(type))
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GenericConcreteEntity,
                type.Locations.FirstOrDefault(), type.ToDisplayString()));

        foreach (var member in type.GetMembers())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (member.IsImplicitlyDeclared || member.Name is not ("Id" or "EntityId" or "EntityIdSource") ||
                member is not (IPropertySymbol or IFieldSymbol or IMethodSymbol or IEventSymbol or INamedTypeSymbol) ||
                !_entity.GetMembers(member.Name).OfType<IPropertySymbol>().Any()) continue;

            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.HiddenEntityIdentityMember,
                member.Locations.FirstOrDefault(), member.ToDisplayString(), member.Name));
        }
    }

    private static bool HasGenericContainerOrParameters(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.ContainingType)
            if (current.Arity > 0) return true;
        return false;
    }

    private void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!_metadataMethods.Contains(method.OriginalDefinition)) return;

        AnalyzeMetadataTypeArguments(context, method);

        foreach (var argumentValue in invocation.Arguments.Select(argument => argument.Value))
        {
            var value = argumentValue;
            while (value is IConversionOperation conversion) value = conversion.Operand;
            if (value is ITypeOfOperation { TypeOperand: INamedTypeSymbol type })
                ReportAbstractEntity(context, method, type);
        }
    }

    private void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var method = ((IMethodReferenceOperation)context.Operation).Method;
        if (_metadataMethods.Contains(method.OriginalDefinition))
            AnalyzeMetadataTypeArguments(context, method);
    }

    private void AnalyzeMetadataTypeArguments(OperationAnalysisContext context, IMethodSymbol method)
    {
        // These overloads inspect entity.GetType(), not typeof(TEntity).
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, _entity) &&
            method.Name is "GetEntityType" or "GetEntityTypeId" or "GetAppId" &&
            method.IsGenericMethod && method.Parameters.Length != 0) return;

        for (var index = 0; index < method.TypeArguments.Length; index++)
        {
            // Validate<TApp>(id, entityType) reads IAppId, not entity metadata.
            if (method.TypeArguments[index] is INamedTypeSymbol argument &&
                method.TypeParameters[index].ConstraintTypes.OfType<INamedTypeSymbol>().Any(IsEntity))
                ReportAbstractEntity(context, method, argument);
        }
    }

    private void ReportAbstractEntity(OperationAnalysisContext context, IMethodSymbol method, INamedTypeSymbol type)
    {
        if (!type.IsAbstract || !IsEntity(type)) return;
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.AbstractEntityMetadataArgument,
            context.Operation.Syntax.GetLocation(), type.ToDisplayString(), method.Name));
    }

    private bool IsEntity(INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(type, _entity) || EntityAnalyzerHelper.DerivesFrom(type, _entity);
}
