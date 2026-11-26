using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ResponseReturnBranchAnalysis
{
    internal static ImmutableArray<IOperation> CreationPropertyConditions(
        IPropertySymbol property, ImmutableArray<IOperation> returnConditions,
        Dictionary<IPropertySymbol, object?>? commonConstants, ImmutableArray<IOperation> enclosingConditions) =>
        commonConstants is not null && commonConstants.ContainsKey(property)
            ? enclosingConditions
            : returnConditions;

    internal static ImmutableArray<IOperation> MergeReturnConditions(
        ImmutableArray<IOperation> enclosing, ImmutableArray<IOperation> branch)
    {
        if (enclosing.IsDefaultOrEmpty) return branch;
        foreach (var condition in branch.AsSpan())
        {
            if (!enclosing.Contains(condition))
                enclosing = enclosing.Add(condition);
        }
        return enclosing;
    }

    internal static bool AreEquivalentConditionalBranches(
        IOperation whenTrue, IOperation whenFalse, Dictionary<IPropertySymbol, object?>? commonConstants)
    {
        var left = RoslynOperationHelpers.UnwrapTransparent(whenTrue) as IObjectCreationOperation;
        var right = RoslynOperationHelpers.UnwrapTransparent(whenFalse) as IObjectCreationOperation;
        if (left?.Type is not INamedTypeSymbol leftType || right?.Type is not INamedTypeSymbol rightType)
            return false;

        var sameTypeAndConstructor =
            SymbolEqualityComparer.Default.Equals(leftType, rightType) &&
            SymbolEqualityComparer.Default.Equals(left.Constructor, right.Constructor);
        if (!sameTypeAndConstructor || !AreEquivalentConstantArguments(left.Arguments, right.Arguments))
            return false;

        var leftCount = left.Initializer?.Initializers.Length ?? 0;
        var rightCount = right.Initializer?.Initializers.Length ?? 0;
        if (leftCount == 0 && rightCount == 0)
            return true;

        var bothHaveCommonInitializers = commonConstants is not null && leftCount > 0 && rightCount > 0;
        return bothHaveCommonInitializers &&
               InitializerMatchesOnlyCommonConstants(left.Initializer!, commonConstants!) &&
               InitializerMatchesOnlyCommonConstants(right.Initializer!, commonConstants!);
    }

    internal static Dictionary<IPropertySymbol, object?>? FindCommonReturnConstants(
        List<SelectorReturn> returns, ConventionFacts facts)
    {
        INamedTypeSymbol? returnType = null;
        Dictionary<IPropertySymbol, object?>? common = null;
        foreach (var returned in returns)
        {
            if (RoslynOperationHelpers.UnwrapTransparent(returned.Value) is not IObjectCreationOperation
                { Type: INamedTypeSymbol type, Initializer: { } initializer })
                return null;
            returnType ??= type;
            if (!SymbolEqualityComparer.Default.Equals(type, returnType))
                return null;
            common = CollectCreationReturnConstants(initializer, type, common, facts);
            if (common is null) return null;
        }
        return common;
    }

    private static bool AreEquivalentConstantArguments(
        ImmutableArray<IArgumentOperation> leftArgs, ImmutableArray<IArgumentOperation> rightArgs)
    {
        if (leftArgs.Length != rightArgs.Length) return false;
        for (var i = 0; i < leftArgs.Length; i++)
        {
            var left = leftArgs[i];
            if (left.Parameter is not { RefKind: RefKind.None } parameter ||
                FindArgumentByOrdinal(rightArgs, parameter.Ordinal) is not { } right)
                return false;
            if (!AreEquivalentConstantArgument(left, right))
                return false;
        }
        return true;
    }

    private static IArgumentOperation? FindArgumentByOrdinal(ImmutableArray<IArgumentOperation> arguments, int ordinal)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Parameter?.Ordinal == ordinal)
                return arguments[i];
        }
        return null;
    }

    private static bool AreEquivalentConstantArgument(IArgumentOperation left, IArgumentOperation right)
    {
        var bothDefaultValues = left.ArgumentKind == ArgumentKind.DefaultValue &&
            right.ArgumentKind == ArgumentKind.DefaultValue;
        if (bothDefaultValues) return true;
        return left.Value.ConstantValue is { HasValue: true, Value: var leftValue } &&
               right.Value.ConstantValue is { HasValue: true, Value: var rightValue } &&
               SameConstant(leftValue, rightValue);
    }

    private static bool InitializerMatchesOnlyCommonConstants(
        IObjectOrCollectionInitializerOperation initializer, Dictionary<IPropertySymbol, object?> commonConstants)
    {
        for (var i = 0; i < initializer.Initializers.Length; i++)
        {
            var isCommonConstantWrite = initializer.Initializers[i] is ISimpleAssignmentOperation
                {
                    Target: IPropertyReferenceOperation { Property.IsIndexer: false } member,
                    Value.ConstantValue.HasValue: true
                } &&
                commonConstants.ContainsKey(member.Property);
            if (!isCommonConstantWrite)
                return false;
        }
        return true;
    }

    private static Dictionary<IPropertySymbol, object?>? CollectCreationReturnConstants(
        IObjectOrCollectionInitializerOperation initializer, INamedTypeSymbol type,
        Dictionary<IPropertySymbol, object?>? common, ConventionFacts facts)
    {
        Dictionary<IPropertySymbol, object?>? matches = null;
        foreach (var write in initializer.Initializers)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (write is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.IsIndexer: false } member } assignment)
                return null;
            var property = PropertyAccessFacts.DispatchedProperty(member.Property, type, facts.Cancellation);
            // A custom setter can change a sibling property assigned earlier in the initializer.
            if (!facts.ProjectionFacts.CanGuaranteeDirectSetter(property, type))
                return null;
            if (!TryMatchCommonConstant(assignment, member.Property, property, type, common, facts, out var value))
            {
                matches?.Remove(member.Property);
                continue;
            }
            (matches ??= new(SymbolEqualityComparer.Default))[member.Property] = value;
        }
        return matches is { Count: > 0 } ? matches : null;
    }

    private static bool TryMatchCommonConstant(
        ISimpleAssignmentOperation assignment, IPropertySymbol writtenProperty, IPropertySymbol dispatched,
        INamedTypeSymbol type, Dictionary<IPropertySymbol, object?>? common, ConventionFacts facts, out object? value)
    {
        value = null;
        if (assignment.Value.ConstantValue is not { HasValue: true, Value: var constant })
            return false;
        var mismatchesPreviousConstant = common is not null &&
            (!common.TryGetValue(writtenProperty, out var previous) || !SameConstant(previous, constant));
        if (mismatchesPreviousConstant)
            return false;
        var getter = facts.ProjectionFacts.ResolveGetterRead(dispatched, type);
        var readsOtherState = getter.MayReadSibling || !getter.Forwarded.IsDefaultOrEmpty;
        if (readsOtherState)
            return false;
        value = constant;
        return true;
    }

    // Signed zero and decimal scale can be distinguished in a serialized response.
    private static bool SameConstant(object? first, object? second) => (first, second) switch
    {
        (float x, float y) => BitConverter.SingleToInt32Bits(x) == BitConverter.SingleToInt32Bits(y),
        (double x, double y) => BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y),
        (decimal x, decimal y) => decimal.GetBits(x).AsSpan().SequenceEqual(decimal.GetBits(y)),
        _ => Equals(first, second)
    };
}
