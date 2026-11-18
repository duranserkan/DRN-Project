using System.Collections.Concurrent;
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

    private sealed class OperationsFacts(Compilation compilation, INamedTypeSymbol entity,
        EntityAnalyzerHelper.EntityTypeFacts entityFacts)
    {
        internal INamedTypeSymbol Entity { get; } = entity;
        internal EntityAnalyzerHelper.EntityTypeFacts EntityFacts { get; } = entityFacts;
        internal INamedTypeSymbol? EntityGeneric { get; } =
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity`1");
        internal INamedTypeSymbol? Aggregate { get; } =
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot");
        internal INamedTypeSymbol? AggregateGeneric { get; } =
            compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot`1");
        internal INamedTypeSymbol? ListType { get; } =
            compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
        internal ConcurrentDictionary<IMethodSymbol, bool> PassiveConstructors { get; } =
            new(SymbolEqualityComparer.Default);
    }

    private readonly Compilation _compilation;
    private readonly CancellationToken _cancellation;
    private readonly Action<Diagnostic> _reportDiagnostic;
    private readonly OperationsFacts _facts;
    private readonly Dictionary<ILocalSymbol, FreshEntity?> _locals = new(SymbolEqualityComparer.Default);
    private readonly List<FreshEntity> _origins = [];
    private FreshEntity? _this;

    private UninitializedEntityOperationsAnalysis(Compilation compilation, CancellationToken cancellation,
        Action<Diagnostic> reportDiagnostic, OperationsFacts facts)
    {
        _compilation = compilation;
        _cancellation = cancellation;
        _reportDiagnostic = reportDiagnostic;
        _facts = facts;
    }

    internal static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol entity,
        EntityAnalyzerHelper.EntityTypeFacts entityFacts)
    {
        var facts = new OperationsFacts(context.Compilation, entity, entityFacts);
        context.RegisterOperationBlockStartAction(start =>
        {
            if (start.OwningSymbol is not IMethodSymbol method) return;
            var hasOperationsCall = 0;
            start.RegisterOperationAction(operation =>
            {
                var invocation = (IInvocationOperation)operation.Operation;
                if (IsOperationsMethod(invocation.TargetMethod, facts.Entity)) Volatile.Write(ref hasOperationsCall, 1);
            }, OperationKind.Invocation);

            // Each diagnostic belongs to the block, including calls outside the first matching invocation.
            start.RegisterOperationBlockEndAction(end =>
            {
                if (Volatile.Read(ref hasOperationsCall) == 0) return;
                IOperation? previousRoot = null;
                foreach (var block in end.OperationBlocks)
                {
                    var root = block;
                    while (root.Parent is { } parent) root = parent;
                    // The constructor initializer and body can share the same root operation.
                    if (ReferenceEquals(root, previousRoot)) continue;
                    previousRoot = root;

                    var analysis = new UninitializedEntityOperationsAnalysis(
                        end.Compilation, end.CancellationToken, end.ReportDiagnostic, facts);
                    analysis.InitializeConstructorReceiver(method);
                    analysis.Evaluate(root);
                }
            });
        });
    }

    private void InitializeConstructorReceiver(IMethodSymbol method)
    {
        if (method.MethodKind == MethodKind.Constructor && IsEntity(method.ContainingType) &&
            IsConstructorInitializationPassive(method, new HashSet<ISymbol>(SymbolEqualityComparer.Default), out _))
            _this = NewOrigin();
    }

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
        _cancellation.ThrowIfCancellationRequested();
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
            IsPassiveConstructor(creation.Constructor))
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
        if (IsOperationsMethod(invocation.TargetMethod, _facts.Entity))
        {
            if (receiver is { Fresh: true } && RequiresOperations(invocation))
                _reportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UninitializedEntityOperations,
                    invocation.Syntax.GetLocation(), invocation.TargetMethod.Name));
            return;
        }
        if (invocation.TargetMethod.MethodKind != MethodKind.Constructor ||
            !IsPassiveConstructor(invocation.TargetMethod)) Forget();
    }

    private static bool IsOperationsMethod(IMethodSymbol method, INamedTypeSymbol entity) => !method.IsStatic &&
        method.Name is "GetEntityId" or "ToSecure" or "ToPlain" &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, entity);

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
        _facts.EntityFacts.IsEntity(type, _cancellation);

    private bool IsFrameworkConstructor(IMethodSymbol constructor)
    {
        var definition = constructor.ContainingType.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, _facts.Entity) ||
               SymbolEqualityComparer.Default.Equals(definition, _facts.EntityGeneric) ||
               SymbolEqualityComparer.Default.Equals(definition, _facts.Aggregate) ||
               SymbolEqualityComparer.Default.Equals(definition, _facts.AggregateGeneric);
    }

    private bool IsPassiveConstructor(IMethodSymbol constructor) =>
        IsPassiveConstructor(constructor, visited: null, out _);

    private bool IsPassiveConstructor(IMethodSymbol constructor, HashSet<ISymbol>? visited, out bool cycleDetected)
    {
        _cancellation.ThrowIfCancellationRequested();
        cycleDetected = false;
        if (IsFrameworkConstructor(constructor)) return true;
        if (_facts.PassiveConstructors.TryGetValue(constructor, out var passive)) return passive;
        visited ??= new(SymbolEqualityComparer.Default);
        if (!visited.Add(constructor.OriginalDefinition))
        {
            cycleDetected = true;
            return false;
        }
        passive = IsConstructorInitializationPassive(constructor, visited, out cycleDetected) &&
                  HasPassiveConstructorBodies(constructor);
        _cancellation.ThrowIfCancellationRequested();
        if (!cycleDetected || passive)
            _facts.PassiveConstructors.TryAdd(constructor, passive);
        return passive;
    }

    private bool HasPassiveConstructorBodies(IMethodSymbol constructor)
    {
        foreach (var reference in constructor.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(_cancellation);
            if (!HasPassiveConstructorBody(syntax)) return false;
        }
        return true;
    }

    private bool HasPassiveConstructorBody(SyntaxNode syntax)
    {
        if (syntax is ClassDeclarationSyntax) return true; // Primary constructor has no separate body.
        if (syntax is not ConstructorDeclarationSyntax declaration) return false;
        var model = _compilation.GetSemanticModel(syntax.SyntaxTree);
        if (declaration.ExpressionBody != null &&
            (model.GetOperation(declaration.ExpressionBody.Expression, _cancellation) is not ISimpleAssignmentOperation expressionAssignment ||
             !IsPassiveAssignment(expressionAssignment)))
            return false;
        if (declaration.Body == null) return true;
        return declaration.Body.Statements.All(statement =>
            model.GetOperation(statement, _cancellation) is IExpressionStatementOperation
                { Operation: ISimpleAssignmentOperation assignment } && IsPassiveAssignment(assignment));
    }

    private bool IsConstructorInitializationPassive(IMethodSymbol constructor, HashSet<ISymbol> visited, out bool cycleDetected)
    {
        cycleDetected = false;
        if (IsFrameworkConstructor(constructor)) return true;
        var type = constructor.ContainingType;
        if (!HasPassiveMemberInitializers(type)) return false;
        return HasPassiveConstructorInitializer(constructor, visited, out cycleDetected);
    }

    private bool HasPassiveMemberInitializers(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length == 0) return false;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var syntax = reference.GetSyntax(_cancellation);
            if (!_compilation.ContainsSyntaxTree(syntax.SyntaxTree)) return false;
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

    private bool HasPassiveConstructorInitializer(IMethodSymbol constructor, HashSet<ISymbol> visited, out bool cycleDetected)
    {
        cycleDetected = false;
        var declaration = constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(_cancellation);
        if (declaration is not (null or ConstructorDeclarationSyntax or ClassDeclarationSyntax)) return false;
        SyntaxNode? initializerSyntax = declaration switch
        {
            ConstructorDeclarationSyntax ordinary => ordinary.Initializer,
            ClassDeclarationSyntax primary => primary.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault(),
            _ => null
        };
        if (initializerSyntax != null)
        {
            var model = _compilation.GetSemanticModel(initializerSyntax.SyntaxTree);
            var arguments = initializerSyntax is ConstructorInitializerSyntax ordinary
                ? ordinary.ArgumentList.Arguments
                : ((PrimaryConstructorBaseTypeSyntax)initializerSyntax).ArgumentList.Arguments;
            if (arguments.Any(argument => !IsPassiveValue(model.GetOperation(argument.Expression, _cancellation)))) return false;
            return model.GetSymbolInfo(initializerSyntax, _cancellation).Symbol is IMethodSymbol target &&
                   IsPassiveConstructor(target, visited, out cycleDetected);
        }
        var type = constructor.ContainingType;
        var candidates = type.BaseType?.InstanceConstructors.Where(candidate =>
            _compilation.IsSymbolAccessibleWithin(candidate, type) &&
            candidate.Parameters.All(parameter => parameter.IsOptional)).ToArray() ?? [];
        var implicitBase = candidates.FirstOrDefault(candidate => candidate.Parameters.Length == 0)
                           ?? (candidates.Length == 1 ? candidates[0] : null);
        return implicitBase != null && IsPassiveConstructor(implicitBase, visited, out cycleDetected);
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
                    reference.GetSyntax(_cancellation) is PropertyDeclarationSyntax
                    { AccessorList: not null } syntax && syntax.AccessorList.Accessors.All(accessor => accessor.Body == null && accessor.ExpressionBody == null)),
            _ => false
        };
    }

    private bool IsPassiveInitializer(EqualsValueClauseSyntax initializer)
    {
        var model = _compilation.GetSemanticModel(initializer.SyntaxTree);
        if (model.GetConstantValue(initializer.Value, _cancellation).HasValue) return true;
        // The standard empty List<T> initializer cannot expose or initialize its owning entity.
        return _facts.ListType != null &&
               initializer.Value is CollectionExpressionSyntax { Elements.Count: 0 } &&
               model.GetTypeInfo(initializer.Value, _cancellation).ConvertedType is INamedTypeSymbol type &&
               SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, _facts.ListType);
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
