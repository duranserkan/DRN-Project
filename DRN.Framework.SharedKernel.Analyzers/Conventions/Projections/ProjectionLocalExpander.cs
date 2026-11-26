using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ProjectionLocalExpander
{
    internal static bool TryPushSelectorLocal(
        ProjectionNode entry,
        ConventionFacts facts,
        Stack<ProjectionNode> pending,
        ref HashSet<LocalExpansionKey>? visited,
        Dictionary<SelectorScopeKey, SelectorFlowResult>? selectorScopes)
    {
        if (entry.Operation is IFieldReferenceOperation field)
        {
            var cursor = LocalAccessClassifier.CreateAccessCursor(entry, field, facts.Symbols);
            var state = CreateState(entry, facts, pending, ref visited, selectorScopes);
            return TryExpandChainedOrBranchingValue(state, field, cursor);
        }
        if (entry.Locals is null) return false;
        if (entry.Operation is ILocalReferenceOperation local && entry.Locals.TryGetValues(local, out var values))
        {
            var cursor = LocalAccessClassifier.CreateAccessCursor(entry, local, facts.Symbols);
            var state = CreateState(entry, facts, pending, ref visited, selectorScopes);
            ExpandBindings(state, values, cursor);
            return true;
        }
        if (entry.IsInput &&
            entry.Operation is IConditionalAccessInstanceOperation placeholder &&
            LocalAccessClassifier.TryResolveConditionalPlaceholderAccess(
                placeholder, facts.Symbols, facts.Cancellation, out var receiver, out var cursorFromPlaceholder))
        {
            var state = CreateState(entry, facts, pending, ref visited, selectorScopes);
            return TryExpandChainedOrBranchingValue(state, receiver, cursorFromPlaceholder);
        }
        return false;
    }

    internal static bool TryMatchWriteTuplePath(
        IOperation written, ImmutableArray<int> readPath, int readIndex, out int nextReadIndex)
    {
        var steps = CollectWriteSteps(written, out var reachedLocal, out var exceededTupleLimit);
        return TryMatchWriteTuplePath(steps, reachedLocal, exceededTupleLimit, readPath, readIndex, out nextReadIndex, out _);
    }

    private static LocalExpansionState CreateState(
        ProjectionNode entry,
        ConventionFacts facts,
        Stack<ProjectionNode> pending,
        ref HashSet<LocalExpansionKey>? visited,
        Dictionary<SelectorScopeKey, SelectorFlowResult>? selectorScopes)
    {
        visited ??= new(LocalExpansionKeyComparer.Instance);
        return new(entry, facts, pending, visited, selectorScopes);
    }

    private static void ExpandBindings(
        LocalExpansionState state, ImmutableArray<LocalBinding> bindings, ExpansionCursor cursor)
    {
        var needsReceiverType = !cursor.Chain.IsDefaultOrEmpty ||
            cursor.AccessMode == LocalAccessMode.RecordNullCheck ||
            (!state.Entry.IsInput && !cursor.HasRemainingTuplePath);
        var receiverType = needsReceiverType
            ? PropertyAccessFacts.KnownLocalReceiverType(bindings.AsSpan())
            : null;
        var supersedesBaseProperty = cursor.HasRemainingProperties &&
            !cursor.HasRemainingTuplePath &&
            PropertyAccessFacts.HasDefinitePropertyReplacement(bindings, cursor.CurrentProperty, receiverType, state.Facts);
        var isDirectDestination = !state.Entry.IsInput &&
            cursor.Chain.IsDefaultOrEmpty &&
            !cursor.HasRemainingTuplePath;
        var suppressedInitializerProperties = isDirectDestination
            ? CollectDefiniteReplacedProperties(bindings, receiverType, state.Facts)
            : default;

        for (var i = bindings.Length - 1; i >= 0; i--)
        {
            var binding = bindings[i];
            if (supersedesBaseProperty && binding.IsBaseValueBinding) continue;
            var appliesSuppression = !suppressedInitializerProperties.IsDefaultOrEmpty && binding.IsBaseValueBinding;
            var bindingState = appliesSuppression
                ? state with
                {
                    Entry = state.Entry.NextWithSuppressed(
                        state.Entry.Operation, state.Entry.Target, state.Entry.IsInput, suppressedInitializerProperties)
                }
                : state;
            ExpandBinding(bindingState, binding, cursor, receiverType);
        }
    }

    private static ImmutableArray<IPropertySymbol> CollectDefiniteReplacedProperties(
        ImmutableArray<LocalBinding> bindings, INamedTypeSymbol? localType, ConventionFacts facts)
    {
        ImmutableArray<IPropertySymbol>.Builder? replaced = null;
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (!binding.ReplacesMember ||
                PropertyAccessFacts.DirectWrittenProperty(binding.WriteTarget) is not { } writtenProperty)
                continue;
            var dispatched = PropertyAccessFacts.DispatchedProperty(writtenProperty, localType, facts.Cancellation);
            if (!facts.ProjectionFacts.CanGuaranteeDirectSetter(dispatched, localType))
                continue;
            if (facts.ProjectionFacts.ResolveGetterRead(dispatched, localType).MayReadSibling)
                continue;
            (replaced ??= ImmutableArray.CreateBuilder<IPropertySymbol>()).Add(dispatched);
        }
        return replaced is null ? [] : replaced.ToImmutable();
    }

    internal static void ExpandBinding(
        LocalExpansionState state, LocalBinding binding, ExpansionCursor cursor, INamedTypeSymbol? localType)
    {
        if (binding.WriteTarget is { } written &&
            !TryApplyWriteTarget(ref state, binding, written, ref cursor, localType))
            return;

        var combined = TuplePathHelpers.Combine(binding.TuplePath, cursor.TuplePath, cursor.TupleIndex);
        var nextCursor = cursor.WithTuplePath(combined);
        switch (binding.Kind)
        {
            case LocalBindingKind.Condition:
                ExpandConditionValue(state, binding.Value);
                return;
            case LocalBindingKind.ReceiverMutation when binding.Value is IInvocationOperation call:
                ExpandReceiverMutation(state, call);
                return;
            case LocalBindingKind.SequenceElement:
                ProjectionSequenceExpander.ExpandSequenceElement(state, binding.Value, nextCursor);
                return;
            default:
                ExpandLocalValue(state, binding.Value, nextCursor);
                return;
        }
    }

    private static bool TryApplyWriteTarget(
        ref LocalExpansionState state, LocalBinding binding, IOperation written,
        ref ExpansionCursor cursor, INamedTypeSymbol? localType)
    {
        // Fast path for the common 1-step direct property assignment (e.g. row.Label = value).
        if (RoslynOperationHelpers.UnwrapTransparent(written) is IPropertyReferenceOperation { Property.IsIndexer: false } directMember &&
            RoslynOperationHelpers.UnwrapTransparent(directMember.Instance) is ILocalReferenceOperation)
            return TryApplyDirectPropertyWriteTarget(ref state, written, directMember, ref cursor, localType);

        // Single walk from written back to local handles tuple matching, property steps, and indexer keys.
        var steps = CollectWriteSteps(written, out var reachedLocal, out var exceededTupleLimit);
        if (!TryMatchWriteTuplePath(steps, reachedLocal, exceededTupleLimit,
                cursor.TuplePath, cursor.TupleIndex, out var nextTupleIndex, out var isPureTupleTarget))
            return false;
        cursor = cursor with { TupleIndex = nextTupleIndex };

        var ignoresNullCheckWrite = CanSkipNullCheckWrite(cursor, localType, state.Facts) &&
            (!isPureTupleTarget || binding.Kind == LocalBindingKind.ReceiverMutation);
        if (ignoresNullCheckWrite) return false;

        var target = state.Entry.Target;
        if (steps is not null &&
            !TryAdvanceWriteMemberSteps(state, written, steps, ref cursor, localType, ref target))
            return false;
        state = state with { Entry = state.Entry with { Target = target } };
        if (binding.Kind != LocalBindingKind.Condition && steps is not null)
            ExpandWriteTargetKeys(state, steps);
        return true;
    }

    private static bool TryApplyDirectPropertyWriteTarget(
        ref LocalExpansionState state, IOperation written, IPropertyReferenceOperation directMember,
        ref ExpansionCursor cursor, INamedTypeSymbol? localType)
    {
        if (CanSkipNullCheckWrite(cursor, localType, state.Facts)) return false;

        var destination = state.Entry.Target;
        if (!TryAdvanceMemberStep(state, written, directMember, ref cursor, localType, ref destination, out _))
            return false;
        state = state with { Entry = state.Entry with { Target = destination } };
        return true;
    }

    private static bool TryAdvanceWriteMemberSteps(
        LocalExpansionState state, IOperation written, List<IOperation> steps,
        ref ExpansionCursor cursor, INamedTypeSymbol? localType, ref ProjectionTarget? target)
    {
        for (var i = steps.Count - 1; i >= 0; i--)
        {
            if (steps[i] is not IPropertyReferenceOperation { Property.IsIndexer: false } member)
                continue;
            if (!TryAdvanceMemberStep(state, written, member, ref cursor, localType, ref target, out var stopMembers))
                return false;
            if (stopMembers) break;
        }
        return true;
    }

    private static bool CanSkipNullCheckWrite(ExpansionCursor cursor, INamedTypeSymbol? localType, ConventionFacts facts)
    {
        if (!cursor.IsNullCheck || cursor.HasRemainingProperties) return false;
        return cursor.AccessMode == LocalAccessMode.NullCheckOnly ||
            (localType is not null && PropertyAccessFacts.HasCompilerGeneratedRecordEquals(localType, facts));
    }

    private static bool TryAdvanceMemberStep(
        LocalExpansionState state, IOperation written, IPropertyReferenceOperation member,
        ref ExpansionCursor cursor, INamedTypeSymbol? localType, ref ProjectionTarget? destination,
        out bool stopMembers)
    {
        stopMembers = false;
        if (cursor.HasRemainingProperties &&
            !TryMatchGetterPropertyStep(state, member, ref cursor, localType, out stopMembers))
            return false;
        if (!state.Entry.IsInput)
            destination = AssignWriteDestination(state, written, member);
        return true;
    }

    private static bool TryMatchGetterPropertyStep(
        LocalExpansionState state, IPropertyReferenceOperation member,
        ref ExpansionCursor cursor, INamedTypeSymbol? localType, out bool stopMembers)
    {
        stopMembers = false;
        var unwrappedInstance = RoslynOperationHelpers.UnwrapTransparent(member.Instance);
        var receiverType = RoslynSymbolHelpers.SealedType(member.Instance?.Type);
        if (receiverType is null && unwrappedInstance is ILocalReferenceOperation)
            receiverType = localType;
        var staticReceiver = unwrappedInstance?.Type as INamedTypeSymbol ?? member.Instance?.Type as INamedTypeSymbol;
        var read = state.Facts.ProjectionFacts.ResolveGetterRead(cursor.CurrentProperty, receiverType);
        cursor = cursor with { ChainIndex = cursor.ChainIndex + 1 };
        if (read.Matches(member.Property, state.Facts.Cancellation, staticReceiver))
            return true;
        if (!read.MayReadSibling) return false;
        cursor = cursor.ResetToFull();
        stopMembers = true;
        return true;
    }

    private static ProjectionTarget AssignWriteDestination(
        LocalExpansionState state, IOperation written, IPropertyReferenceOperation member)
    {
        var site = written.Parent is IAssignmentOperation or IInvocationOperation ? written.Parent : written;
        var destination = new ProjectionTarget(member.Property, PropertyAccessFacts.Receiver(member, state.Facts),
            member.Instance?.Type, site.Syntax.GetLocation());
        if (!state.Entry.ReturnConditions.IsDefaultOrEmpty)
        {
            var conditions = state.Entry.ReturnConditions;
            for (var i = conditions.Length - 1; i >= 0; i--)
                state.Pending.Push(new(conditions[i], destination, true, state.Entry.Locals));
        }
        return destination;
    }

    private static void ExpandWriteTargetKeys(LocalExpansionState state, List<IOperation> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var part = steps[i];
            if (part is IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
            {
                foreach (var key in indexer.Arguments)
                    ExpandConditionValue(state, key.Value);
            }
            else if (part is IArrayElementReferenceOperation element)
            {
                foreach (var index in element.Indices)
                    ExpandConditionValue(state, index);
            }
        }
    }

    private static List<IOperation>? CollectWriteSteps(
        IOperation written, out bool reachedLocal, out bool exceededTupleLimit)
    {
        List<IOperation>? steps = null;
        reachedLocal = false;
        exceededTupleLimit = false;
        var depth = 0;
        for (var part = RoslynOperationHelpers.UnwrapTransparent(written);
             part is not null;
             part = CollectionMutationFacts.MutationReceiver(part))
        {
            if (++depth > ProjectionLimits.MaxTupleDepth + 1)
                exceededTupleLimit = true;
            if (part is ILocalReferenceOperation)
            {
                reachedLocal = true;
                break;
            }
            (steps ??= new(4)).Add(part);
        }
        return steps;
    }

    private static bool TryMatchWriteTuplePath(
        List<IOperation>? steps, bool reachedLocal, bool exceededTupleLimit,
        ImmutableArray<int> readPath, int readIndex, out int nextReadIndex, out bool isPureTupleTarget)
    {
        nextReadIndex = readIndex;
        isPureTupleTarget = false;
        if (!reachedLocal || exceededTupleLimit || steps is null)
            return true;

        var hasReadPath = !readPath.IsDefault && readIndex < readPath.Length;
        var remainingRead = hasReadPath ? readPath.Length - readIndex : 0;
        var matchedCount = 0;
        var sawTupleField = false;
        var pureTuple = true;

        for (var i = steps.Count - 1; i >= 0; i--)
        {
            var ordinal = steps[i] is IFieldReferenceOperation field
                ? RoslynSymbolHelpers.TupleElementOrdinal(field.Field)
                : -1;
            if (ordinal < 0)
            {
                pureTuple = false;
                break;
            }
            sawTupleField = true;
            if (matchedCount < remainingRead)
            {
                if (ordinal != readPath[readIndex + matchedCount])
                    return false;
                matchedCount++;
            }
        }

        isPureTupleTarget = pureTuple && sawTupleField;
        nextReadIndex = readIndex + matchedCount;
        return true;
    }

    private static void ExpandReceiverMutation(LocalExpansionState state, IInvocationOperation call)
    {
        var target = state.Entry.Target;
        var arguments = call.Arguments;
        for (var i = arguments.Length - 1; i >= 0; i--)
        {
            var argument = arguments[i];
            if (argument.Parameter?.RefKind == RefKind.Out) continue;
            var storesElement = !state.Entry.IsInput &&
                CollectionMutationFacts.StoresProjectedElement(call, argument, state.Entry.Locals, state.Facts);
            var key = new LocalExpansionKey(argument, target?.Property, target?.TargetReceiver, target?.View,
                !storesElement, LocalAccessMode.Full, default, 0, default, 0,
                ReturnConditions: storesElement ? state.Entry.ReturnConditions : default);
            if (state.Visited.Add(key))
                state.Pending.Push(state.Entry.Next(argument.Value, target, !storesElement));
        }
    }

    internal static void ExpandConditionValue(LocalExpansionState state, IOperation rawValue)
    {
        var value = RoslynOperationHelpers.UnwrapTransparentNonNull(rawValue);
        var target = state.Entry.Target;
        var key = new LocalExpansionKey(value, target?.Property, target?.TargetReceiver, target?.View,
            IsInput: true, LocalAccessMode.Full, default, 0, default, 0);
        if (state.Visited.Add(key))
            state.Pending.Push(state.Entry.Next(value, target, true));
    }

    internal static void ExpandLocalValue(LocalExpansionState state, IOperation rawValue, ExpansionCursor cursor)
    {
        state.Facts.Cancellation.ThrowIfCancellationRequested();
        var value = RoslynOperationHelpers.UnwrapTransparentNonNull(rawValue);
        var target = state.Entry.Target;
        var key = new LocalExpansionKey(value, target?.Property, target?.TargetReceiver, target?.View,
            state.Entry.IsInput, cursor.AccessMode, cursor.Chain, cursor.ChainIndex, cursor.TuplePath, cursor.TupleIndex,
            state.Entry.SuppressedInitializerProperties, state.Entry.ReturnConditions);
        if (!state.Visited.Add(key)) return;
        if (cursor.Depth >= ProjectionLimits.MaxExpansionDepth)
        {
            state.Pending.Push(state.Entry.Next(value, target, state.Entry.IsInput));
            return;
        }
        if (cursor.HasRemainingTuplePath && TryExpandTupleStep(state, value, cursor))
            return;
        if (!state.Entry.IsInput || cursor.AccessMode == LocalAccessMode.Full)
        {
            var preservesSuppressedProperties = !state.Entry.IsInput &&
                value is IObjectCreationOperation &&
                !state.Entry.SuppressedInitializerProperties.IsDefaultOrEmpty;
            var nextNode = preservesSuppressedProperties
                ? state.Entry.NextWithSuppressed(value, target, false, state.Entry.SuppressedInitializerProperties)
                : state.Entry.Next(value, target, state.Entry.IsInput);
            state.Pending.Push(nextNode);
            return;
        }
        if (TryExpandChainedOrBranchingValue(state, value, cursor.ClearTuplePath()))
            return;
        if (cursor.HasRemainingProperties)
        {
            ExpandPropertyStep(state, value, cursor);
            return;
        }
        if (IsIgnoredNullCheckCreation(cursor.AccessMode, value, state.Facts))
            return;
        state.Pending.Push(state.Entry.Next(value, target, true));
    }

    private static bool IsIgnoredNullCheckCreation(LocalAccessMode accessMode, IOperation value, ConventionFacts facts)
    {
        if (accessMode == LocalAccessMode.NullCheckOnly)
            return value is IObjectCreationOperation or IWithOperation or IObjectOrCollectionInitializerOperation;
        if (accessMode != LocalAccessMode.RecordNullCheck)
            return false;
        return value switch
        {
            IObjectCreationOperation { Type: INamedTypeSymbol createdType } =>
                PropertyAccessFacts.HasCompilerGeneratedRecordEquals(createdType, facts),
            IWithOperation { Type: INamedTypeSymbol { IsSealed: true } sealedType } =>
                PropertyAccessFacts.HasCompilerGeneratedRecordEquals(sealedType, facts),
            _ => false
        };
    }

    private static bool TryExpandTupleStep(LocalExpansionState state, IOperation value, ExpansionCursor cursor)
    {
        if (value is ITupleOperation tuple)
        {
            var index = cursor.CurrentTupleOrdinal;
            if (index >= 0 && index < tuple.Elements.Length)
            {
                ExpandLocalValue(state, tuple.Elements[index], cursor.NextTupleElement());
                return true;
            }
        }
        return TryExpandChainedOrBranchingValue(state, value, cursor);
    }

    private static bool TryExpandChainedOrBranchingValue(
        LocalExpansionState state, IOperation value, ExpansionCursor cursor)
    {
        if (TryExpandTupleFieldOrPatternInput(state, value, cursor)) return true;
        if (value is ILocalReferenceOperation chainedLocal && state.Entry.Locals is not null &&
            state.Entry.Locals.TryGetValues(chainedLocal, out var chainedValues))
        {
            ExpandBindings(state, chainedValues, cursor.NextDepth());
            return true;
        }
        return TryExpandBranchingValue(state, value, cursor);
    }

    private static bool TryExpandTupleFieldOrPatternInput(
        LocalExpansionState state, IOperation value, ExpansionCursor cursor)
    {
        if (value is IFieldReferenceOperation field)
        {
            var ordinal = RoslynSymbolHelpers.TupleElementOrdinal(field.Field);
            if (ordinal >= 0 && ProjectionOriginAnalyzer.MemberReceiverOrigin(field) is { } tupleReceiver)
            {
                var combined = TuplePathHelpers.Combine([ordinal], cursor.TuplePath, cursor.TupleIndex);
                if (!combined.IsDefaultOrEmpty)
                {
                    combined = TuplePathHelpers.Combine(tupleReceiver.TuplePath, combined, 0);
                    ExpandLocalValue(state, tupleReceiver.Value, cursor.WithDepthTuplePath(combined));
                    return true;
                }
            }
        }
        if (!cursor.HasRemainingTuplePath) return false;
        if (value is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.PatternInput } input &&
            ProjectionOriginAnalyzer.PatternInputOrigin(input) is { } origin)
        {
            var combined = TuplePathHelpers.Combine(origin.TuplePath, cursor.TuplePath, cursor.TupleIndex);
            ExpandLocalValue(state, origin.Value, cursor.WithDepthTuplePath(combined));
            return true;
        }
        if (value is IPropertyReferenceOperation { Instance: { } nullable } property &&
            RoslynOperationHelpers.IsNullableValueProperty(property))
        {
            ExpandLocalValue(state, nullable, cursor.NextDepth());
            return true;
        }
        return false;
    }

    private static bool TryExpandBranchingValue(
        LocalExpansionState state, IOperation value, ExpansionCursor cursor)
    {
        if (value is IConditionalOperation conditional)
        {
            state.Pending.Push(state.Entry.Next(conditional.Condition, state.Entry.Target, true));
            if (conditional.WhenFalse is { } whenFalse)
                ExpandLocalValue(state, whenFalse, cursor.NextDepth());
            ExpandLocalValue(state, conditional.WhenTrue, cursor.NextDepth());
            return true;
        }
        if (value is ICoalesceOperation coalesce)
        {
            ExpandLocalValue(state, coalesce.WhenNull, cursor.NextDepth());
            ExpandLocalValue(state, coalesce.Value, cursor.NextDepth());
            return true;
        }
        if (value is ISwitchExpressionOperation switchExpression)
        {
            if (!ProjectionOriginAnalyzer.AreAllArmsBindingOnly(switchExpression.Arms))
                state.Pending.Push(state.Entry.Next(switchExpression.Value, state.Entry.Target, true));
            for (var i = switchExpression.Arms.Length - 1; i >= 0; i--)
            {
                var arm = switchExpression.Arms[i];
                if (arm.Guard is { } guard)
                    state.Pending.Push(state.Entry.Next(guard, state.Entry.Target, true));
                state.Pending.Push(state.Entry.Next(arm.Pattern, state.Entry.Target, true));
                ExpandLocalValue(state, arm.Value, cursor.NextDepth());
            }
            return true;
        }
        return false;
    }

    private static void ExpandPropertyStep(LocalExpansionState state, IOperation value, ExpansionCursor cursor)
    {
        var property = cursor.CurrentProperty;
        switch (value)
        {
            case IObjectCreationOperation creation:
                var created = state.Facts.ProjectionFacts.ResolveGetterRead(property, creation.Type as INamedTypeSymbol);
                ExpandCreationPropertyStep(state, creation, created, cursor);
                return;
            case IWithOperation withOp:
                var withReceiver = RoslynSymbolHelpers.SealedType(value.Type);
                var copied = state.Facts.ProjectionFacts.ResolveGetterRead(property, withReceiver);
                if (!TryExpandInitializerProperty(state, withOp.Initializer, copied, withReceiver, cursor))
                {
                    if (copied.MayReadSibling)
                        state.Pending.Push(state.Entry.Next(withOp.Initializer, state.Entry.Target, true));
                    ExpandLocalValue(state, withOp.Operand, cursor.NextDepth());
                }
                return;
            case IObjectOrCollectionInitializerOperation initializer:
                var nestedReceiver = RoslynSymbolHelpers.SealedType(value.Type);
                var nested = state.Facts.ProjectionFacts.ResolveGetterRead(property, nestedReceiver);
                if (!TryExpandInitializerProperty(state, initializer, nested, nestedReceiver, cursor) && nested.MayReadSibling)
                    state.Pending.Push(state.Entry.Next(value, state.Entry.Target, true));
                return;
            default:
                state.Pending.Push(state.Entry.Next(value, state.Entry.Target, true));
                return;
        }
    }

    private static void ExpandCreationPropertyStep(
        LocalExpansionState state, IObjectCreationOperation creation, GetterRead accessed, ExpansionCursor cursor)
    {
        var receiverType = creation.Type as INamedTypeSymbol;
        if (creation.Initializer is { } initializer &&
            TryExpandInitializerProperty(state, initializer, accessed, receiverType, cursor))
            return;
        if (!accessed.MayReadSibling &&
            TryExpandVerifiedCreationArguments(state, creation, accessed, cursor))
            return;
        if (accessed.MayReadSibling)
        {
            state.Pending.Push(state.Entry.Next(creation, state.Entry.Target, true));
            return;
        }
        for (var i = creation.Arguments.Length - 1; i >= 0; i--)
        {
            var argument = creation.Arguments[i];
            if (argument.ArgumentKind != ArgumentKind.DefaultValue)
                state.Pending.Push(state.Entry.Next(argument.Value, state.Entry.Target, true));
        }
    }

    private static bool TryExpandVerifiedCreationArguments(
        LocalExpansionState state, IObjectCreationOperation creation, GetterRead accessed, ExpansionCursor cursor)
    {
        if (creation.Type is not INamedTypeSymbol { TypeKind: not TypeKind.Error } destinationType)
            return false;
        var matchedArg = false;
        for (var i = creation.Arguments.Length - 1; i >= 0; i--)
        {
            var argument = creation.Arguments[i];
            if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
            var destinationProperty = PropertyAccessFacts.FindVerifiedPositionalRecordProperty(
                destinationType, creation.Constructor, argument.Parameter, state.Facts.Cancellation);
            if (destinationProperty is null || !accessed.Matches(destinationProperty, state.Facts.Cancellation, destinationType))
                continue;
            matchedArg = true;
            ExpandLocalValue(state, argument.Value, cursor.NextProperty());
        }
        return matchedArg;
    }

    private static bool TryExpandInitializerProperty(
        LocalExpansionState state, IObjectOrCollectionInitializerOperation initializer,
        GetterRead accessed, INamedTypeSymbol? receiverType, ExpansionCursor cursor)
    {
        var replaced = false;
        for (var i = initializer.Initializers.Length - 1; i >= 0; i--)
        {
            var entry = initializer.Initializers[i];
            var (written, assignedValue) = ResponseProjectionAnalysis.ReadWrite(entry);
            if (assignedValue is null || written is not IPropertyReferenceOperation property ||
                !accessed.Matches(property.Property, state.Facts.Cancellation, receiverType))
                continue;
            ExpandLocalValue(state, assignedValue, cursor.NextProperty());
            var canMarkReplaced = !replaced && entry is ISimpleAssignmentOperation && !accessed.MayReadSibling;
            if (canMarkReplaced)
            {
                var dispatched = PropertyAccessFacts.DispatchedProperty(property.Property, receiverType, state.Facts.Cancellation);
                if (state.Facts.ProjectionFacts.CanGuaranteeDirectSetter(dispatched, receiverType))
                    replaced = true;
            }
        }
        return replaced;
    }
}
