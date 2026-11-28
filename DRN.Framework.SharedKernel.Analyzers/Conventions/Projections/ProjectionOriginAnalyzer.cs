using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ProjectionOriginAnalyzer
{
    internal static bool HasEntityOrDtoOrigin(
        ProjectionCheckContext context, IPropertyReferenceOperation source, SelectorScope? locals)
    {
        if (MemberReceiverOrigin(source) is { } origin &&
            HasEntityOrDtoOriginOperation(context, origin, locals))
            return true;
        return IsEntityOrDto(context.Facts, PropertyAccessFacts.Receiver(source, context.Facts));
    }

    internal static bool IsEntityOrDto(ConventionFacts facts, INamedTypeSymbol type) =>
        type.TypeKind != TypeKind.Error &&
        (facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation) ||
         facts.IsEntity(type));

    internal static bool IsEntityOrDtoType(ConventionFacts facts, ITypeSymbol? type, int depth)
    {
        if (type is null || depth >= 8) return false;
        if (type is INamedTypeSymbol named) return IsEntityOrDto(facts, named);
        if (type is not ITypeParameterSymbol parameter) return false;
        var constraints = parameter.ConstraintTypes;
        for (var i = 0; i < constraints.Length; i++)
        {
            if (IsEntityOrDtoType(facts, RoslynSymbolHelpers.UnwrapNullable(constraints[i]), depth + 1))
                return true;
        }
        return false;
    }

    internal static LocalBinding? MemberReceiverOrigin(IMemberReferenceOperation member)
    {
        if (member.Instance is { } instance) return new(instance);
        var isSubpatternMember = member.Parent is IPropertySubpatternOperation subpattern &&
            ReferenceEquals(subpattern.Member, member);
        return isSubpatternMember ? PatternContainerMatchedValue((IPropertySubpatternOperation)member.Parent!) : null;
    }

    internal static LocalBinding? PatternInputOrigin(IInstanceReferenceOperation patternInput)
    {
        for (var current = patternInput.Parent; current is not null; current = current.Parent)
        {
            if (current is IPropertySubpatternOperation subpattern)
                return PatternContainerMatchedValue(subpattern);
            if (current is IMemberReferenceOperation or IConversionOperation or IParenthesizedOperation)
                continue;
            return PatternContainerMatchedValue(patternInput);
        }
        return null;
    }

    internal static LocalBinding? PatternContainerMatchedValue(IOperation start)
    {
        Stack<int>? indices = null;
        for (var child = start; child.Parent is { } parent; child = parent)
        {
            if (parent is IPatternOperation)
            {
                TryPushTupleSubpatternIndex(parent, child, ref indices);
                continue;
            }
            if (PatternContainerValue(parent) is not { } value) return null;
            ImmutableArray<int> tuplePath = indices is null ? [] : [.. indices];
            return new LocalBinding(value, tuplePath);
        }
        return null;
    }

    private static void TryPushTupleSubpatternIndex(IOperation parent, IOperation child, ref Stack<int>? indices)
    {
        if (parent is not IRecursivePatternOperation { MatchedType: INamedTypeSymbol { IsTupleType: true } } tuple ||
            child is not IPatternOperation part)
            return;
        var index = tuple.DeconstructionSubpatterns.IndexOf(part);
        if (index >= 0) (indices ??= new()).Push(index);
    }

    private static IOperation? PatternContainerValue(IOperation parent) => parent switch
    {
        IPropertySubpatternOperation subpattern => subpattern.Member,
        IIsPatternOperation isPattern => isPattern.Value,
        ISwitchExpressionArmOperation { Parent: ISwitchExpressionOperation expression } => expression.Value,
        IPatternCaseClauseOperation { Parent: ISwitchCaseOperation { Parent: ISwitchOperation statement } } => statement.Value,
        _ => null
    };

    internal static bool IsBindingOnlyPattern(IPatternOperation pattern)
    {
        // Fast path avoids allocating a stack for common single-node patterns.
        if (IsUnconditionalBindingPattern(pattern))
            return true;
        if (pattern is not IRecursivePatternOperation { InputType: INamedTypeSymbol { IsTupleType: true } })
            return false;

        var pending = new Stack<IPatternOperation>();
        pending.Push(pattern);
        while (pending.TryPop(out var part))
        {
            if (IsUnconditionalBindingPattern(part)) continue;
            if (part is not IRecursivePatternOperation { InputType: INamedTypeSymbol { IsTupleType: true } } tuple)
                return false;
            if (!TryPushTuplePatternParts(tuple, pending))
                return false;
        }
        return true;
    }

    private static bool IsUnconditionalBindingPattern(IPatternOperation pattern) =>
        pattern is IDeclarationPatternOperation { MatchesNull: true } or IDiscardPatternOperation;

    private static bool TryPushTuplePatternParts(IRecursivePatternOperation tuple, Stack<IPatternOperation> pending)
    {
        foreach (var member in tuple.PropertySubpatterns)
        {
            var isTupleFieldPattern = member.Member is IFieldReferenceOperation
                { Instance: null or IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.PatternInput } } field &&
                RoslynSymbolHelpers.TupleElementOrdinal(field.Field) >= 0;
            if (!isTupleFieldPattern) return false;
            pending.Push(member.Pattern);
        }
        foreach (var element in tuple.DeconstructionSubpatterns)
            pending.Push(element);
        return true;
    }

    internal static bool AreAllArmsBindingOnly(ImmutableArray<ISwitchExpressionArmOperation> arms)
    {
        for (var i = 0; i < arms.Length; i++)
        {
            if (!IsBindingOnlyPattern(arms[i].Pattern))
                return false;
        }
        return true;
    }

    private static bool HasEntityOrDtoOriginOperation(
        ProjectionCheckContext context, LocalBinding start, SelectorScope? locals)
    {
        var facts = context.Facts;
        facts.Cancellation.ThrowIfCancellationRequested();

        // Fast path for direct entity/DTO reads without touching traversal collections.
        var unwrappedStart = RoslynOperationHelpers.UnwrapInstance(start.Value, facts.Cancellation);
        if (unwrappedStart is null) return false;
        if (start.TuplePath.IsDefaultOrEmpty &&
            IsEntityOrDtoType(facts, RoslynSymbolHelpers.UnwrapNullable(unwrappedStart.Type), 0))
            return true;

        var pending = context.OriginPending;
        var visited = context.OriginVisited;
        pending.Clear();
        visited.Clear();
        try
        {
            pending.Push(new(new(unwrappedStart, start.TuplePath)));
            var steps = 0;
            while (pending.TryPop(out var entry) && steps < ProjectionLimits.MaxOriginSteps)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                var current = RoslynOperationHelpers.UnwrapInstance(entry.Binding.Value, facts.Cancellation);
                if (current is null) continue;
                var normalized = new OriginEntry(new(current, entry.Binding.TuplePath), entry.ArrayIndex);
                if (!visited.Add(normalized)) continue;
                steps++;

                var hasTuplePath = !normalized.Binding.TuplePath.IsDefaultOrEmpty;
                if (hasTuplePath && TryPeelOriginTuple(current, normalized.Binding.TuplePath, normalized.ArrayIndex, locals, pending))
                    continue;
                if (IsEntityOrDtoType(facts, RoslynSymbolHelpers.UnwrapNullable(current.Type), 0))
                    return true;
                PushOriginPredecessors(current, normalized.Binding.TuplePath, normalized.ArrayIndex, locals, facts, pending);
            }
            return false;
        }
        finally
        {
            pending.Clear();
            visited.Clear();
        }
    }

    private static bool TryPeelOriginTuple(
        IOperation current, ImmutableArray<int> tuplePath, int? arrayIndex, SelectorScope? locals, Stack<OriginEntry> pending)
    {
        if (current is ITupleOperation tuple)
        {
            var index = tuplePath[0];
            if (index >= 0 && index < tuple.Elements.Length)
            {
                var rest = tuplePath.Length == 1 ? default : tuplePath.RemoveAt(0);
                pending.Push(new(new(tuple.Elements[index], rest), arrayIndex));
                return true;
            }
        }
        if (current is ILocalReferenceOperation localRef && locals is not null &&
            locals.TryGetValues(localRef, out var localValues))
        {
            PushLocalOriginTupleBindings(localValues, tuplePath, arrayIndex, pending);
            return true;
        }
        return TryPushBranchingOriginPredecessors(current, tuplePath, arrayIndex, pending);
    }

    private static void PushLocalOriginTupleBindings(
        ImmutableArray<LocalBinding> localValues, ImmutableArray<int> tuplePath, int? arrayIndex, Stack<OriginEntry> pending)
    {
        for (var i = 0; i < localValues.Length; i++)
        {
            var value = localValues[i];
            if (value.Kind == LocalBindingKind.Condition) continue;
            var nextIndex = 0;
            if (value.WriteTarget is { } written &&
                !ProjectionLocalExpander.TryMatchWriteTuplePath(written, tuplePath, 0, out nextIndex))
                continue;
            var nextArrayIndex = value.WriteTarget is null ? arrayIndex : null;
            pending.Push(new(new(value.Value, TuplePathHelpers.Combine(value.TuplePath, tuplePath, nextIndex)), nextArrayIndex));
        }
    }

    private static void PushOriginPredecessors(
        IOperation current, ImmutableArray<int> tuplePath, int? arrayIndex, SelectorScope? locals,
        ConventionFacts facts, Stack<OriginEntry> pending)
    {
        if (current is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.PatternInput } patternInput &&
            PatternInputOrigin(patternInput) is { } pattern)
        {
            pending.Push(new(new(pattern.Value, TuplePathHelpers.Combine(pattern.TuplePath, tuplePath, 0)), arrayIndex));
            return;
        }
        if (current is IMemberReferenceOperation member && MemberReceiverOrigin(member) is { } receiver)
        {
            var suffix = MemberOriginTupleSuffix(member, tuplePath, out var preservesArrayIndex);
            pending.Push(new(
                new(receiver.Value, TuplePathHelpers.Combine(receiver.TuplePath, suffix, 0)),
                preservesArrayIndex ? arrayIndex : null));
            return;
        }
        if (TryPushPreservedSequenceOriginPredecessors(current, facts.Symbols, pending))
            return;
        if (TryPushLiteralCollectionOriginPredecessors(current, tuplePath, arrayIndex, pending))
            return;
        if (current is IArrayElementReferenceOperation arrayElement)
        {
            pending.Push(new(new(arrayElement.ArrayReference, tuplePath), TryGetConstantArrayIndex(arrayElement)));
            return;
        }
        if (SingleOriginPredecessor(current, facts) is { } single)
        {
            pending.Push(new(new(single)));
            return;
        }
        if (current is ILocalReferenceOperation localRef && locals is not null &&
            locals.TryGetValues(localRef, out var localValues))
        {
            for (var i = 0; i < localValues.Length; i++)
            {
                var value = localValues[i];
                if (value.Kind != LocalBindingKind.Condition)
                    pending.Push(new(value, value.WriteTarget is null ? arrayIndex : null));
            }
            return;
        }
        TryPushBranchingOriginPredecessors(current, default, arrayIndex, pending);
    }

    private static ImmutableArray<int> MemberOriginTupleSuffix(
        IMemberReferenceOperation member, ImmutableArray<int> tuplePath, out bool preservesArrayIndex)
    {
        if (member is IFieldReferenceOperation field)
        {
            var ordinal = RoslynSymbolHelpers.TupleElementOrdinal(field.Field);
            if (ordinal >= 0)
            {
                preservesArrayIndex = true;
                return TuplePathHelpers.Combine([ordinal], tuplePath, 0);
            }
        }
        if (member is IPropertyReferenceOperation property && RoslynOperationHelpers.IsNullableValueProperty(property))
        {
            preservesArrayIndex = true;
            return tuplePath;
        }
        preservesArrayIndex = false;
        return [];
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning call Arguments directly avoids boxing ImmutableArray, allocating a capturing predicate and creating a Where iterator.")]
    private static bool TryPushPreservedSequenceOriginPredecessors(
        IOperation current, ConventionSymbols symbols, Stack<OriginEntry> pending)
    {
        if (current is not IInvocationOperation { Instance: null } call ||
            !LinqSequenceFacts.TryGetPreservedSequenceArgumentCount(call.TargetMethod, symbols, out var preservedCount))
            return false;
        var sourceTypeParameter = LinqSequenceFacts.FallbackElementTypeParameter(call.TargetMethod);
        foreach (var argument in call.Arguments)
        {
            if (LinqSequenceFacts.PreservesSequenceArgument(argument, preservedCount, sourceTypeParameter))
                pending.Push(new(new(argument.Value)));
        }
        return true;
    }

    private static bool TryPushLiteralCollectionOriginPredecessors(
        IOperation current, ImmutableArray<int> tuplePath, int? arrayIndex, Stack<OriginEntry> pending)
    {
        if (current is IArrayCreationOperation { Initializer: { } initializer } arrayCreation)
        {
            if (arrayIndex is { } index &&
                arrayCreation.Type is IArrayTypeSymbol { Rank: 1 } &&
                index >= 0 && index < initializer.ElementValues.Length)
            {
                pending.Push(new(new(initializer.ElementValues[index], tuplePath)));
                return true;
            }
            foreach (var element in initializer.ElementValues)
                pending.Push(new(new(element, tuplePath)));
            return true;
        }
        if (current is ICollectionExpressionOperation collection)
        {
            if (arrayIndex is { } index &&
                TryGetFixedCollectionElement(collection, index) is { } element)
            {
                pending.Push(new(new(element, tuplePath)));
                return true;
            }
            foreach (var item in collection.Elements)
                pending.Push(new(new(item is ISpreadOperation spread ? spread.Operand : item, tuplePath)));
            return true;
        }
        return false;
    }

    private static IOperation? TryGetFixedCollectionElement(ICollectionExpressionOperation collection, int index)
    {
        if (index < 0 || index >= collection.Elements.Length)
            return null;
        for (var i = 0; i <= index; i++)
        {
            if (collection.Elements[i] is ISpreadOperation)
                return null;
        }
        return collection.Elements[index];
    }

    private static int? TryGetConstantArrayIndex(IArrayElementReferenceOperation arrayElement)
    {
        if (arrayElement.Indices is not [var indexOp] || indexOp.ConstantValue is not { HasValue: true, Value: var value })
            return null;
        return value switch
        {
            int i when i >= 0 => i,
            long l when l is >= 0 and <= int.MaxValue => (int)l,
            uint u when u <= int.MaxValue => (int)u,
            ulong ul when ul <= int.MaxValue => (int)ul,
            short s when s >= 0 => s,
            ushort us => us,
            sbyte sb when sb >= 0 => sb,
            byte b => b,
            char c => c,
            _ => null
        };
    }

    private static IOperation? SingleOriginPredecessor(IOperation current, ConventionFacts facts) => current switch
    {
        IInvocationOperation call => RoslynOperationHelpers.TryGetExtensionReceiver(call),
        IParameterReferenceOperation parameter => SelectSource(parameter, facts),
        _ => null
    };

    private static IOperation? SelectSource(IParameterReferenceOperation reference, ConventionFacts facts)
    {
        var isFirstLambdaParameter = reference.Parameter.Ordinal == 0 &&
            reference.Parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction };
        if (!isFirstLambdaParameter) return null;
        var owner = (IMethodSymbol)reference.Parameter.ContainingSymbol;
        for (var current = reference.Parent; current is not null; current = current.Parent)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (current is not IAnonymousFunctionOperation lambda ||
                !SymbolEqualityComparer.Default.Equals(lambda.Symbol, owner)) continue;
            var call = LinqSequenceFacts.FindEnclosingLinqCall(lambda, facts.Symbols);
            return call is not null && LinqSequenceFacts.IsSelectOrOrdering(call.TargetMethod, facts.Symbols) &&
                   ReferenceEquals(LinqSequenceFacts.SelectLambda(call), lambda)
                ? RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 0)
                : null;
        }
        return null;
    }

    private static bool TryPushBranchingOriginPredecessors(
        IOperation current, ImmutableArray<int> tuplePath, int? arrayIndex, Stack<OriginEntry> pending)
    {
        if (current is IConditionalOperation conditional)
        {
            pending.Push(new(new(conditional.WhenTrue, tuplePath), arrayIndex));
            if (conditional.WhenFalse is { } whenFalse) pending.Push(new(new(whenFalse, tuplePath), arrayIndex));
            return true;
        }
        if (current is ICoalesceOperation coalesce)
        {
            pending.Push(new(new(coalesce.Value, tuplePath), arrayIndex));
            pending.Push(new(new(coalesce.WhenNull, tuplePath), arrayIndex));
            return true;
        }
        if (current is ISwitchExpressionOperation switchExpression)
        {
            foreach (var arm in switchExpression.Arms) pending.Push(new(new(arm.Value, tuplePath), arrayIndex));
            return true;
        }
        return false;
    }
}
