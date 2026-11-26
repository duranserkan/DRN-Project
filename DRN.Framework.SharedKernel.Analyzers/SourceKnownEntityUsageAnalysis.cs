using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class SourceKnownEntityUsageAnalysis
{
    private const string Domain = "DRN.Framework.SharedKernel.Domain.";
    private const string ValidateMethodName = "Validate";
    private readonly INamedTypeSymbol _entity;
    private readonly EntityAnalyzerHelper.EntityTypeFacts _entityFacts;
    private readonly INamedTypeSymbol? _identity;
    private readonly INamedTypeSymbol? _format;
    private readonly INamedTypeSymbol? _repositoryInterface;
    private readonly INamedTypeSymbol? _repositoryBase;
    private readonly HashSet<long> _formats;
    private readonly bool _hasId;
    private readonly bool _hasEntityId;
    private readonly bool _hasEntityIdSource;
    private readonly HashSet<ISymbol> _metadataMethods = new(SymbolEqualityComparer.Default);
    private readonly SymbolFactCache<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> _repositoryBindings = new();
    private readonly Func<INamedTypeSymbol, CancellationToken, ImmutableArray<INamedTypeSymbol>> _findBindings;

    private SourceKnownEntityUsageAnalysis(Compilation compilation, INamedTypeSymbol entity,
        EntityAnalyzerHelper.EntityTypeFacts entityFacts)
    {
        _entity = entity;
        _entityFacts = entityFacts;
        _findBindings = FindRepositoryBindings;
        _repositoryInterface = compilation.GetTypeByMetadataName(Domain + "Repository.ISourceKnownRepository`1");
        _repositoryBase = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Domain.SourceKnownRepository`2");
        _identity = compilation.GetTypeByMetadataName(Domain + "EntityTypeId");
        _format = compilation.GetTypeByMetadataName(Domain + "SourceKnownEntityIdFormat");
        _formats = [];
        if (_format != null)
        {
            foreach (var field in _format.GetMembers().OfType<IFieldSymbol>())
                if (field.HasConstantValue && TryToInt64(field.ConstantValue, out var value))
                    _formats.Add(value);
        }
        _hasId = _entity.GetMembers("Id").OfType<IPropertySymbol>().Any();
        _hasEntityId = _entity.GetMembers("EntityId").OfType<IPropertySymbol>().Any();
        _hasEntityIdSource = _entity.GetMembers("EntityIdSource").OfType<IPropertySymbol>().Any();

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

    internal static SourceKnownEntityUsageAnalysis Register(CompilationStartAnalysisContext context, INamedTypeSymbol entity,
        EntityAnalyzerHelper.EntityTypeFacts entityFacts)
    {
        var analysis = new SourceKnownEntityUsageAnalysis(context.Compilation, entity, entityFacts);
        context.RegisterOperationAction(analysis.AnalyzeArgument, OperationKind.Argument);
        context.RegisterOperationAction(analysis.AnalyzeAssignment, OperationKind.SimpleAssignment);
        context.RegisterOperationAction(analysis.AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(analysis.AnalyzeMethodReference, OperationKind.MethodReference);
        if (analysis._repositoryInterface != null || analysis._repositoryBase != null)
            context.RegisterSyntaxNodeAction(analysis.AnalyzeRepositoryType, SyntaxKind.GenericName, SyntaxKind.IdentifierName);
        return analysis;
    }

    private void AnalyzeRepositoryType(SyntaxNodeAnalysisContext context)
    {
        if (!MayBindRepositoryType(context.Node)) return;
        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;
        if (symbol is IAliasSymbol alias) symbol = alias.Target;
        if (symbol is not INamedTypeSymbol type || type.IsUnboundGenericType) return;
        // Roslyn binds an inferred var to its initializer's type. Only explicit repository names are use sites.
        if (context.Node is IdentifierNameSyntax { IsVar: true } identifier && type.Name != "var" &&
            context.SemanticModel.GetAliasInfo(identifier, context.CancellationToken) is null) return;

        var bindings = _repositoryBindings.GetOrAdd(type, _findBindings, context.CancellationToken);
        foreach (var binding in bindings)
            ReportRepositoryBinding(context, type, binding);
    }

    private static bool MayBindRepositoryType(SyntaxNode node)
    {
        if (node is not IdentifierNameSyntax identifier)
            return node.Parent is not NameEqualsSyntax;

        var current = ClimbQualifiedName(identifier, out var parent);
        if (parent is BaseNamespaceDeclarationSyntax or NameEqualsSyntax or NameColonSyntax)
            return false;
        if (parent is UsingDirectiveSyntax usingDirective && usingDirective.Alias is null &&
            usingDirective.StaticKeyword.RawKind == 0)
            return false;

        if (ReferenceEquals(current, identifier) && IsMemberOrInvocationName(identifier, parent)) return false;

        if (parent is ArgumentSyntax) return IsInsideNameof(parent);
        if (parent is BinaryExpressionSyntax binary)
            return binary.Right == current && binary.Kind() is SyntaxKind.IsExpression or SyntaxKind.AsExpression;

        return parent is not (
            AssignmentExpressionSyntax or EqualsValueClauseSyntax or ReturnStatementSyntax or
            ArrowExpressionClauseSyntax or ThrowStatementSyntax or IfStatementSyntax or WhileStatementSyntax or
            ConditionalExpressionSyntax or PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax or
            AwaitExpressionSyntax or InterpolationSyntax or InitializerExpressionSyntax or
            ElementAccessExpressionSyntax or ExpressionStatementSyntax);
    }

    private static bool IsMemberOrInvocationName(IdentifierNameSyntax identifier, SyntaxNode? parent)
    {
        if (parent is InvocationExpressionSyntax invocation) return invocation.Expression == identifier;
        return parent is MemberAccessExpressionSyntax access && access.Name == identifier &&
               access.Parent is not (MemberAccessExpressionSyntax or ConstantPatternSyntax or BinaryExpressionSyntax) &&
               !IsInsideNameof(access.Parent);
    }

    private static SyntaxNode ClimbQualifiedName(SyntaxNode node, out SyntaxNode? parent)
    {
        var current = node;
        while (current.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax)
            current = current.Parent;
        parent = current.Parent;
        return current;
    }

    private static bool IsInsideNameof(SyntaxNode? node)
    {
        for (; node is not null; node = node.Parent)
        {
            if (node is ArgumentSyntax &&
                node.Parent is ArgumentListSyntax
                {
                    Parent: InvocationExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                    }
                })
                return true;
            if (node is StatementSyntax or MemberDeclarationSyntax or LambdaExpressionSyntax)
                return false;
        }
        return false;
    }

    private ImmutableArray<INamedTypeSymbol> FindRepositoryBindings(INamedTypeSymbol type, CancellationToken cancellation)
    {
        HashSet<ISymbol>? reported = null;
        var bindings = Check(type, null);
        for (var current = type.BaseType; current != null; current = current.BaseType) bindings = Check(current, bindings);
        foreach (var implemented in type.AllInterfaces) bindings = Check(implemented, bindings);
        return bindings?.ToImmutable() ?? [];

        ImmutableArray<INamedTypeSymbol>.Builder? Check(INamedTypeSymbol binding, ImmutableArray<INamedTypeSymbol>.Builder? collected)
        {
            cancellation.ThrowIfCancellationRequested();
            var index = RepositoryEntityIndex(binding);
            if (index < 0 || binding.TypeArguments[index] is not INamedTypeSymbol { IsAbstract: true } entity ||
                !_entityFacts.IsEntity(entity, cancellation)) return collected;
            reported ??= new(SymbolEqualityComparer.Default);
            if (!reported.Add(entity)) return collected;
            collected ??= ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            collected.Add(binding);
            return collected;
        }
    }

    private int RepositoryEntityIndex(INamedTypeSymbol binding)
    {
        if (SymbolEqualityComparer.Default.Equals(binding.OriginalDefinition, _repositoryInterface)) return 0;
        return SymbolEqualityComparer.Default.Equals(binding.OriginalDefinition, _repositoryBase) ? 1 : -1;
    }

    private void ReportRepositoryBinding(SyntaxNodeAnalysisContext context, INamedTypeSymbol usedType, INamedTypeSymbol binding)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var index = RepositoryEntityIndex(binding);
        var entity = binding.TypeArguments[index];
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
        var isFormat = _format != null &&
                       SymbolEqualityComparer.Default.Equals(RoslynSymbolHelpers.UnwrapNullable(type), _format);
        if ((!isAppId && !isFormat) || ReadConstant(value) is not { } constant) return;
        if (isAppId && constant is byte appId && appId > EntityAnalyzerHelper.MaxAppId)
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidAppIdArgument, value.Syntax.GetLocation(), appId));
        else if (isFormat && TryToInt64(constant, out var formatValue) && !_formats.Contains(formatValue))
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidEntityIdFormatArgument, value.Syntax.GetLocation(), constant));
    }

    private static object? ReadConstant(IOperation value, int depth = 0)
    {
        if (value.ConstantValue.HasValue) return value.ConstantValue.Value;
        if (depth == 32 || value is not IConversionOperation { OperatorMethod: null, IsTryCast: false } conversion ||
            conversion.Operand.Type?.IsValueType != true) return null;

        var target = RoslynSymbolHelpers.UnwrapNullable(conversion.Type);
        var operand = ReadConstant(conversion.Operand, depth + 1);
        if (operand is null || SymbolEqualityComparer.Default.Equals(target, RoslynSymbolHelpers.UnwrapNullable(conversion.Operand.Type)))
            return operand;

        // Roslyn leaves lifted casts unfolded. Apply each intermediate storage conversion in order.
        var storage = target is INamedTypeSymbol { EnumUnderlyingType: { } storageType } ? storageType : target;
        return storage is null ? null : IntegralConstant.ConvertValue(operand, storage.SpecialType, conversion.IsChecked);
    }

    private static bool TryToInt64(object? rawValue, out long value)
    {
        switch (rawValue)
        {
            case sbyte v: value = v; return true;
            case byte v: value = v; return true;
            case short v: value = v; return true;
            case ushort v: value = v; return true;
            case int v: value = v; return true;
            case uint v: value = v; return true;
            case long v: value = v; return true;
            case ulong v when v <= long.MaxValue: value = (long)v; return true;
            default: value = 0; return false;
        }
    }

    internal void AnalyzeHiddenIdentityMembers(SymbolAnalysisContext context, INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (member.IsImplicitlyDeclared || !IsIdentityMember(member.Name) ||
                member is not (IPropertySymbol or IFieldSymbol or IMethodSymbol or IEventSymbol or INamedTypeSymbol)) continue;

            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.HiddenEntityIdentityMember,
                member.Locations.FirstOrDefault(), member.ToDisplayString(), member.Name));
        }
    }

    private bool IsIdentityMember(string name) => name switch
    {
        "Id" => _hasId,
        "EntityId" => _hasEntityId,
        "EntityIdSource" => _hasEntityIdSource,
        _ => false
    };

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
                HasEntityConstraint(method.TypeParameters[index], context.CancellationToken))
                ReportAbstractEntity(context, method, argument);
        }
    }

    private void ReportAbstractEntity(OperationAnalysisContext context, IMethodSymbol method, INamedTypeSymbol type)
    {
        if (!type.IsAbstract || !_entityFacts.IsEntity(type, context.CancellationToken)) return;
        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.AbstractEntityMetadataArgument,
            context.Operation.Syntax.GetLocation(), type.ToDisplayString(), method.Name));
    }

    private bool HasEntityConstraint(ITypeParameterSymbol parameter, CancellationToken cancellation)
    {
        foreach (var constraint in parameter.ConstraintTypes)
            if (constraint is INamedTypeSymbol type && _entityFacts.IsEntity(type, cancellation)) return true;
        return false;
    }
}
