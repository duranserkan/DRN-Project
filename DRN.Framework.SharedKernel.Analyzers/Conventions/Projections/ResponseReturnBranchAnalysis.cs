using Microsoft.CodeAnalysis.CSharp;
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
        if (returns.Count < 2 || RoslynOperationHelpers.UnwrapTransparent(returns[0].Value) is not
            IObjectCreationOperation { Type: INamedTypeSymbol type } first)
            return null;

        Dictionary<IPropertySymbol, object?>? common = null;
        HashSet<IPropertySymbol>? written = null;
        var sameConstructorValues = true;
        for (var i = 0; i < returns.Count; i++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (RoslynOperationHelpers.UnwrapTransparent(returns[i].Value) is not IObjectCreationOperation creation ||
                !SymbolEqualityComparer.Default.Equals(creation.Type, type))
                return null;
            if (i > 0 && sameConstructorValues)
                sameConstructorValues = SymbolEqualityComparer.Default.Equals(first.Constructor, creation.Constructor) &&
                    AreEquivalentConstantArguments(first.Arguments, creation.Arguments);
            if (!IntersectInitializerConstants(creation, type, facts, i == 0, ref common, ref written))
                return null;
        }

        if (!sameConstructorValues || (facts.Role(type).Role != ConventionArchitecture.Contract && !facts.IsLocal(type)))
            return common;

        // Every common initializer is also in written. Equal counts mean every write agrees across branches.
        var equivalentInitializers = written is null || common?.Count == written.Count;
        foreach (var argument in first.Arguments)
        {
            if (facts.ProjectionFacts.FindDestinationProperty(type, argument.Parameter) is not { } property ||
                written?.Contains(property) == true || (!equivalentInitializers && ReadsOtherPropertyState(property, type, facts)))
                continue;
            (common ??= new(SymbolEqualityComparer.Default))[property] = null;
        }
        return common;
    }

    private static bool ReadsOtherPropertyState(
        IPropertySymbol property, INamedTypeSymbol type, ConventionFacts facts)
    {
        var dispatched = PropertyAccessFacts.DispatchedProperty(property, type, facts.Cancellation);
        var getter = facts.ProjectionFacts.ResolveGetterRead(dispatched, type);
        return getter.MayReadSibling || !getter.Forwarded.IsDefaultOrEmpty;
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
        // Ordinary omitted arguments use the same parameter default at every call site.
        if (left.ArgumentKind == ArgumentKind.DefaultValue && right.ArgumentKind == ArgumentKind.DefaultValue &&
            !HasCallerInfo(left.Parameter))
            return true;

        var first = ConstantOperand(left.Value);
        var second = ConstantOperand(right.Value);
        return first.ConstantValue is { HasValue: true, Value: var x } &&
               second.ConstantValue is { HasValue: true, Value: var y } && SameConstant(x, y) &&
               (x is null || SymbolEqualityComparer.Default.Equals(first.Type, second.Type));
    }

    internal static bool HasCallerInfo(IParameterSymbol? parameter) => parameter is not null &&
        parameter.GetAttributes().Any(attribute => attribute.AttributeClass is
            { Name: "CallerLineNumberAttribute" or "CallerFilePathAttribute" or "CallerMemberNameAttribute" or "CallerArgumentExpressionAttribute" } type &&
            RoslynSymbolHelpers.IsNamespace(type.ContainingNamespace, "CompilerServices", "Runtime", "System"));

    private static IOperation ConstantOperand(IOperation value)
    {
        while (value is IConversionOperation { IsTryCast: false } cast && !value.ConstantValue.HasValue)
        {
            var conversion = cast.GetConversion();
            var wrapsNullable = conversion.IsNullable && SymbolEqualityComparer.Default.Equals(
                RoslynSymbolHelpers.UnwrapNullable(cast.Type), cast.Operand.Type);
            if (!conversion.IsIdentity && !conversion.IsBoxing && !(conversion.IsImplicit && conversion.IsReference) && !wrapsNullable)
                break;
            value = cast.Operand;
        }
        return value;
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

    private static bool IntersectInitializerConstants(
        IObjectCreationOperation creation, INamedTypeSymbol type, ConventionFacts facts, bool first,
        ref Dictionary<IPropertySymbol, object?>? common, ref HashSet<IPropertySymbol>? written)
    {
        Dictionary<IPropertySymbol, object?>? matches = null;
        foreach (var write in creation.Initializer?.Initializers ?? [])
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (write is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.IsIndexer: false } member } assignment)
                return false;
            var property = PropertyAccessFacts.DispatchedProperty(member.Property, type, facts.Cancellation);
            // A custom setter can change a sibling property assigned earlier in the initializer.
            if (!facts.ProjectionFacts.CanGuaranteeDirectSetter(property, type))
                return false;
            (written ??= new(SymbolEqualityComparer.Default)).Add(member.Property);
            if ((first || common is not null) && TryMatchCommonConstant(assignment, member.Property, property, type, common, facts, out var value))
                (matches ??= new(SymbolEqualityComparer.Default))[member.Property] = value;
            else
                matches?.Remove(member.Property);
        }
        common = matches;
        return true;
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
