using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

/// <summary>Proves fresh receivers only within straight-line code; unknown effects discard proof.</summary>
internal sealed class UninitializedEntityOperationsAnalysis
{
    private sealed class FreshEntity
    {
        internal bool Fresh = true;
    }

    private readonly OperationBlockAnalysisContext _context;
    private readonly INamedTypeSymbol _entity;
    private readonly Dictionary<ILocalSymbol, FreshEntity?> _locals = new(SymbolEqualityComparer.Default);
    private readonly List<FreshEntity> _origins = [];
    private FreshEntity? _this;

    private UninitializedEntityOperationsAnalysis(OperationBlockAnalysisContext context, INamedTypeSymbol entity)
    {
        _context = context;
        _entity = entity;
    }

    internal static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol entity) =>
        context.RegisterOperationBlockAction(blockContext =>
        {
            if (blockContext.OwningSymbol is not IMethodSymbol method) return;
            foreach (var block in blockContext.OperationBlocks)
            {
                var analysis = new UninitializedEntityOperationsAnalysis(blockContext, entity);
                if (method.MethodKind == MethodKind.Constructor && analysis.IsEntity(method.ContainingType) &&
                    analysis.HasPassiveInitialization(method, new HashSet<ISymbol>(SymbolEqualityComparer.Default)))
                    analysis._this = analysis.NewOrigin();
                analysis.Evaluate(block);
            }
        });

    private FreshEntity NewOrigin()
    {
        var origin = new FreshEntity();
        _origins.Add(origin);
        return origin;
    }

    private void Forget()
    {
        foreach (var origin in _origins) origin.Fresh = false;
        _locals.Clear();
        _origins.Clear();
    }

    private FreshEntity? Evaluate(IOperation? operation)
    {
        _context.CancellationToken.ThrowIfCancellationRequested();
        switch (operation)
        {
            case null: return null;
            case IBlockOperation block:
                foreach (var child in block.Operations)
                {
                    Evaluate(child);
                    if (child is IReturnOperation or IThrowOperation or IBranchOperation) break;
                }
                return null;
            case IMethodBodyOperation body:
                Evaluate(body.BlockBody);
                Evaluate(body.ExpressionBody);
                return null;
            case IConstructorBodyOperation body:
                Evaluate(body.Initializer);
                Evaluate(body.BlockBody);
                Evaluate(body.ExpressionBody);
                return null;
            case IExpressionStatementOperation statement: return Evaluate(statement.Operation);
            case IVariableDeclarationGroupOperation group:
                foreach (var declaration in group.Declarations) Evaluate(declaration);
                return null;
            case IVariableDeclarationOperation declaration:
                foreach (var declarator in declaration.Declarators) Evaluate(declarator);
                return null;
            case IVariableDeclaratorOperation declarator:
                if (declarator.Symbol.RefKind != RefKind.None) { Forget(); return null; }
                return _locals[declarator.Symbol] = Evaluate(declarator.Initializer?.Value);
            case ILocalReferenceOperation local: return _locals.GetValueOrDefault(local.Local);
            case IInstanceReferenceOperation instance when instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance:
                return _this;
            case IParenthesizedOperation parentheses: return Evaluate(parentheses.Operand);
            case IConversionOperation { OperatorMethod: null } conversion: return Evaluate(conversion.Operand);
            case IObjectCreationOperation creation:
                return EvaluateCreation(creation);
            case ISimpleAssignmentOperation assignment:
                return EvaluateAssignment(assignment);
            case IInvocationOperation invocation:
                EvaluateInvocation(invocation);
                return null;
            case IReturnOperation returned:
                Evaluate(returned.ReturnedValue);
                Forget();
                return null;
            case ILiteralOperation or IParameterReferenceOperation or IDefaultValueOperation or ITypeOfOperation or INameOfOperation:
                return null;
            case IFieldReferenceOperation field when field.Field.HasConstantValue:
                return null;
            default:
                // Do not interpret branches, loops, exceptions, captures, awaits, or arbitrary getters.
                Forget();
                return null;
        }
    }

    private FreshEntity? EvaluateCreation(IObjectCreationOperation creation)
    {
        foreach (var argument in creation.Arguments) Evaluate(argument.Value);
        // An initializer can inject operations, invoke setters, or expose the new instance.
        if (creation.Initializer == null && creation.Constructor != null &&
            creation.Type is INamedTypeSymbol type && IsEntity(type) &&
            IsPassiveConstructor(creation.Constructor, new HashSet<ISymbol>(SymbolEqualityComparer.Default)))
            return NewOrigin();
        Forget();
        return null;
    }

    private FreshEntity? EvaluateAssignment(ISimpleAssignmentOperation assignment)
    {
        if (assignment.Target is ILocalReferenceOperation target)
        {
            if (target.Local.RefKind != RefKind.None) { Forget(); return null; }
            return _locals[target.Local] = Evaluate(assignment.Value);
        }
        Evaluate(assignment.Value);
        if (!IsPassiveAssignment(assignment)) Forget();
        return null;
    }

    private void EvaluateInvocation(IInvocationOperation invocation)
    {
        var receiver = Evaluate(invocation.Instance);
        foreach (var argument in invocation.Arguments) Evaluate(argument.Value);
        if (IsOperationsMethod(invocation.TargetMethod))
        {
            if (receiver is { Fresh: true } && RequiresOperations(invocation))
                _context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UninitializedEntityOperations,
                    invocation.Syntax.GetLocation(), invocation.TargetMethod.Name));
            return;
        }
        if (invocation.TargetMethod.MethodKind != MethodKind.Constructor ||
            !IsPassiveConstructor(invocation.TargetMethod, new HashSet<ISymbol>(SymbolEqualityComparer.Default))) Forget();
    }

    private bool IsOperationsMethod(IMethodSymbol method) => !method.IsStatic &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, _entity) &&
        method.Name is "GetEntityId" or "ToSecure" or "ToPlain";

    private static bool RequiresOperations(IInvocationOperation invocation)
    {
        var input = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0);
        if (input == null) return false;
        if (input.Parameter!.Type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T) return true;
        if (input.Value.ConstantValue.HasValue) return input.Value.ConstantValue.Value != null;
        // Unknown nullable inputs might short-circuit. A conversion from a non-nullable value cannot.
        return input.Value is IConversionOperation { Operand.Type: { IsValueType: true } type } &&
               type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
    }

    private bool IsEntity(INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(type, _entity) || EntityAnalyzerHelper.DerivesFrom(type, _entity);

    private bool IsFrameworkConstructor(IMethodSymbol constructor)
    {
        var type = constructor.ContainingType;
        if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _entity.ContainingAssembly)) return false;
        return type.OriginalDefinition.ToDisplayString() is
            "DRN.Framework.SharedKernel.Domain.SourceKnownEntity" or
            "DRN.Framework.SharedKernel.Domain.SourceKnownEntity<TModel>" or
            "DRN.Framework.SharedKernel.Domain.AggregateRoot" or
            "DRN.Framework.SharedKernel.Domain.AggregateRoot<TModel>";
    }

    private bool IsPassiveConstructor(IMethodSymbol constructor, HashSet<ISymbol> visited)
    {
        _context.CancellationToken.ThrowIfCancellationRequested();
        if (IsFrameworkConstructor(constructor)) return true;
        if (!visited.Add(constructor.OriginalDefinition)) return false;
        if (!HasPassiveInitialization(constructor, visited)) return false;
        foreach (var reference in constructor.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(_context.CancellationToken);
            if (!HasPassiveConstructorBody(syntax)) return false;
        }
        return true;
    }

    private bool HasPassiveConstructorBody(SyntaxNode syntax)
    {
        if (syntax is ClassDeclarationSyntax) return true; // Primary constructor has no separate body.
        if (syntax is not ConstructorDeclarationSyntax declaration) return false;
        var model = _context.Compilation.GetSemanticModel(syntax.SyntaxTree);
        if (declaration.ExpressionBody != null &&
            (model.GetOperation(declaration.ExpressionBody.Expression, _context.CancellationToken) is not ISimpleAssignmentOperation expressionAssignment ||
             !IsPassiveAssignment(expressionAssignment)))
            return false;
        if (declaration.Body == null) return true;
        return declaration.Body.Statements.All(statement =>
            model.GetOperation(statement, _context.CancellationToken) is IExpressionStatementOperation
                { Operation: ISimpleAssignmentOperation assignment } && IsPassiveAssignment(assignment));
    }

    private bool HasPassiveInitialization(IMethodSymbol constructor, HashSet<ISymbol> visited)
    {
        if (IsFrameworkConstructor(constructor)) return true;
        var type = constructor.ContainingType;
        if (!HasPassiveMemberInitializers(type)) return false;
        return HasPassiveConstructorInitializer(constructor, visited);
    }

    private bool HasPassiveMemberInitializers(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length == 0) return false;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(_context.CancellationToken);
            if (!_context.Compilation.ContainsSyntaxTree(syntax.SyntaxTree)) return false;
            var initializers = syntax.ChildNodes().OfType<MemberDeclarationSyntax>()
                .SelectMany(GetMemberInitializers).Where(initializer => initializer != null);
            if (!initializers.All(initializer => IsPassiveInitializer(initializer!))) return false;
        }
        return true;
    }

    private static IEnumerable<EqualsValueClauseSyntax?> GetMemberInitializers(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Initializer),
        EventFieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Initializer),
        PropertyDeclarationSyntax property => [property.Initializer],
        _ => []
    };

    private bool HasPassiveConstructorInitializer(IMethodSymbol constructor, HashSet<ISymbol> visited)
    {
        var declaration = constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(_context.CancellationToken);
        if (declaration is not (null or ConstructorDeclarationSyntax or ClassDeclarationSyntax)) return false;
        SyntaxNode? initializerSyntax = declaration switch
        {
            ConstructorDeclarationSyntax ordinary => ordinary.Initializer,
            ClassDeclarationSyntax primary => primary.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault(),
            _ => null
        };
        if (initializerSyntax != null)
        {
            var model = _context.Compilation.GetSemanticModel(initializerSyntax.SyntaxTree);
            var arguments = initializerSyntax is ConstructorInitializerSyntax ordinary
                ? ordinary.ArgumentList.Arguments
                : ((PrimaryConstructorBaseTypeSyntax)initializerSyntax).ArgumentList.Arguments;
            if (arguments.Any(argument => !IsPassiveValue(model.GetOperation(argument.Expression, _context.CancellationToken)))) return false;
            return model.GetSymbolInfo(initializerSyntax, _context.CancellationToken).Symbol is IMethodSymbol target &&
                   IsPassiveConstructor(target, visited);
        }
        var type = constructor.ContainingType;
        var candidates = type.BaseType?.InstanceConstructors.Where(candidate =>
            _context.Compilation.IsSymbolAccessibleWithin(candidate, type) &&
            candidate.Parameters.All(parameter => parameter.IsOptional)).ToArray() ?? [];
        var implicitBase = candidates.FirstOrDefault(candidate => candidate.Parameters.Length == 0)
                           ?? (candidates.Length == 1 ? candidates[0] : null);
        return implicitBase != null && IsPassiveConstructor(implicitBase, visited);
    }

    private bool IsPassiveAssignment(ISimpleAssignmentOperation assignment)
    {
        if (!IsPassiveValue(assignment.Value)) return false;
        return assignment.Target switch
        {
            IFieldReferenceOperation { Instance: IInstanceReferenceOperation, Field.IsStatic: false } field =>
                field.Field.Name is not ("EntityIdOps" or "EntityIdSource"),
            IPropertyReferenceOperation { Instance: IInstanceReferenceOperation } property =>
                !property.Property.IsVirtual && !property.Property.IsOverride &&
                property.Property.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(_context.CancellationToken) is PropertyDeclarationSyntax
                    { AccessorList: not null } syntax && syntax.AccessorList.Accessors.All(accessor => accessor.Body == null && accessor.ExpressionBody == null)),
            _ => false
        };
    }

    private bool IsPassiveInitializer(EqualsValueClauseSyntax initializer)
    {
        var model = _context.Compilation.GetSemanticModel(initializer.SyntaxTree);
        if (model.GetConstantValue(initializer.Value, _context.CancellationToken).HasValue) return true;
        // The standard empty List<T> initializer cannot expose or initialize its owning entity.
        return initializer.Value is CollectionExpressionSyntax { Elements.Count: 0 } &&
               model.GetTypeInfo(initializer.Value, _context.CancellationToken).ConvertedType is INamedTypeSymbol type &&
               SymbolEqualityComparer.Default.Equals(type.OriginalDefinition,
                   _context.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"));
    }

    private static bool IsPassiveValue(IOperation? value) => value switch
    {
        null => false,
        _ when value.ConstantValue.HasValue => true,
        IParameterReferenceOperation => true,
        IConversionOperation { OperatorMethod: null } conversion => IsPassiveValue(conversion.Operand),
        IParenthesizedOperation parentheses => IsPassiveValue(parentheses.Operand),
        _ => false
    };
}
