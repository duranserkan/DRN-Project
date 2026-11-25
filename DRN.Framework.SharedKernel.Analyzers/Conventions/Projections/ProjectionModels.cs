using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Operations;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationContract;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ProjectionLimits
{
    internal const int MaxOriginSteps = 128;
    internal const int MaxTupleDepth = 16;
    internal const int MaxExpansionDepth = 64;
}

internal readonly record struct ProjectionTarget(
    IPropertySymbol Property,
    INamedTypeSymbol TargetReceiver,
    ITypeSymbol? View,
    Location Location);

internal readonly record struct ProjectionSource(
    IPropertySymbol Property,
    INamedTypeSymbol SourceReceiver,
    ITypeSymbol? View = null);

internal sealed class ProjectionCheckContext(
    ConventionFacts facts,
    INamedTypeSymbol ownerType,
    ClassificationContract classifications)
{
    internal ConventionFacts Facts { get; } = facts;
    internal INamedTypeSymbol OwnerType { get; } = ownerType;
    internal ClassificationContract Classifications { get; } = classifications;
    internal Dictionary<IPropertySymbol, PropertyClassifications> Reported { get; } = new(SymbolEqualityComparer.Default);
    internal Stack<LocalBinding> OriginPending { get; } = [];
    internal HashSet<LocalBinding> OriginVisited { get; } = new(LocalBindingComparer.Instance);
}

internal readonly record struct ProjectionNode(
    IOperation Operation,
    ProjectionTarget? Target,
    bool IsInput,
    SelectorScope? Locals = null)
{
    internal ProjectionNode Next(IOperation operation, ProjectionTarget? target, bool isInput) =>
        new(operation, target, isInput, Locals);
}

internal enum LocalAccessMode : byte
{
    Full = 0,
    PropertyChain = 1,
    NullCheckOnly = 2
}

internal enum LocalBindingKind : byte
{
    Value,
    Condition,
    ReceiverMutation,
    SequenceElement
}

internal readonly record struct LocalBinding(
    IOperation Value,
    ImmutableArray<int> TuplePath = default,
    LocalBindingKind Kind = LocalBindingKind.Value,
    IOperation? WriteTarget = null,
    bool ReplacesMember = false)
{
    internal bool IsBaseValueBinding =>
        WriteTarget is null && Kind is LocalBindingKind.Value or LocalBindingKind.SequenceElement;
}

internal sealed class LocalBindingComparer : IEqualityComparer<LocalBinding>
{
    internal static readonly LocalBindingComparer Instance = new();

    public bool Equals(LocalBinding x, LocalBinding y) =>
        ReferenceEquals(x.Value, y.Value) &&
        x.Kind == y.Kind &&
        ReferenceEquals(x.WriteTarget, y.WriteTarget) &&
        x.ReplacesMember == y.ReplacesMember &&
        TuplePathHelpers.SameTail(x.TuplePath, 0, y.TuplePath, 0);

    public int GetHashCode(LocalBinding obj)
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(obj.Value));
        hash.Add(obj.Kind);
        if (obj.WriteTarget is not null) hash.Add(RuntimeHelpers.GetHashCode(obj.WriteTarget));
        hash.Add(obj.ReplacesMember);
        if (!obj.TuplePath.IsDefault)
        {
            foreach (var index in obj.TuplePath) hash.Add(index);
        }
        return hash.ToHashCode();
    }
}

internal readonly record struct SelectorFlowResult(
    List<IOperation> Returns,
    List<IOperation>? ReturnConditions,
    SelectorScope? Scope);

internal sealed record SelectorScope(
    Dictionary<ILocalReferenceOperation, ImmutableArray<LocalBinding>> ReferenceValues,
    Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> CapturedLocalValues,
    Dictionary<IAnonymousFunctionOperation, Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>>> LambdaCapturedValues,
    SelectorScope? Parent)
{
    internal static readonly Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> EmptyCaptured =
        new(SymbolEqualityComparer.Default);
    internal static readonly Dictionary<IAnonymousFunctionOperation, Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>>> EmptyLambdaCaptures = [];

    internal bool TryGetValues(ILocalReferenceOperation reference, out ImmutableArray<LocalBinding> values)
    {
        for (var current = this; current is not null; current = current.Parent)
        {
            if (current.ReferenceValues.TryGetValue(reference, out values))
                return true;
        }
        for (var current = this; current is not null; current = current.Parent)
        {
            if (current.CapturedLocalValues.TryGetValue(reference.Local, out values))
                return true;
        }
        values = default;
        return false;
    }

    internal SelectorScope ForChild(IAnonymousFunctionOperation childLambda) =>
        LambdaCapturedValues.TryGetValue(childLambda, out var exact)
            ? new SelectorScope(ReferenceValues, exact, EmptyLambdaCaptures, Parent)
            : this;
}

internal readonly record struct ExpansionCursor(
    LocalAccessMode AccessMode,
    ImmutableArray<IPropertySymbol> Chain = default,
    int ChainIndex = 0,
    ImmutableArray<int> TuplePath = default,
    int TupleIndex = 0,
    int Depth = 0)
{
    internal bool HasRemainingProperties => !Chain.IsDefault && ChainIndex < Chain.Length;
    internal bool HasRemainingTuplePath => !TuplePath.IsDefault && TupleIndex < TuplePath.Length;
    internal IPropertySymbol CurrentProperty => Chain[ChainIndex];
    internal int CurrentTupleOrdinal => TuplePath[TupleIndex];

    internal ExpansionCursor NextDepth() => this with { Depth = Depth + 1 };
    internal ExpansionCursor NextProperty() => this with { ChainIndex = ChainIndex + 1, Depth = Depth + 1 };
    internal ExpansionCursor NextTupleElement() => this with { TupleIndex = TupleIndex + 1, Depth = Depth + 1 };
    internal ExpansionCursor WithTuplePath(ImmutableArray<int> tuplePath, int tupleIndex = 0) =>
        this with { TuplePath = tuplePath, TupleIndex = tupleIndex };
    internal ExpansionCursor WithDepthTuplePath(ImmutableArray<int> tuplePath, int tupleIndex = 0) =>
        this with { TuplePath = tuplePath, TupleIndex = tupleIndex, Depth = Depth + 1 };
    internal ExpansionCursor ClearTuplePath() => this with { TuplePath = default, TupleIndex = 0 };
    internal ExpansionCursor ResetToFull() => this with { AccessMode = LocalAccessMode.Full, Chain = default, ChainIndex = 0 };
}

internal readonly record struct LocalExpansionKey(
    IOperation Value,
    IPropertySymbol? TargetProperty,
    INamedTypeSymbol? TargetReceiver,
    ITypeSymbol? TargetView,
    bool IsInput,
    LocalAccessMode AccessMode,
    ImmutableArray<IPropertySymbol> PropertyChain,
    int ChainIndex,
    ImmutableArray<int> TuplePath,
    int TupleIndex);

internal sealed class LocalExpansionKeyComparer : IEqualityComparer<LocalExpansionKey>
{
    internal static readonly LocalExpansionKeyComparer Instance = new();

    public bool Equals(LocalExpansionKey x, LocalExpansionKey y) =>
        ReferenceEquals(x.Value, y.Value) &&
        x.IsInput == y.IsInput &&
        x.AccessMode == y.AccessMode &&
        SymbolEqualityComparer.Default.Equals(x.TargetProperty, y.TargetProperty) &&
        SymbolEqualityComparer.Default.Equals(x.TargetReceiver, y.TargetReceiver) &&
        SymbolEqualityComparer.Default.Equals(x.TargetView, y.TargetView) &&
        SamePropertyChainTail(x.PropertyChain, x.ChainIndex, y.PropertyChain, y.ChainIndex) &&
        TuplePathHelpers.SameTail(x.TuplePath, x.TupleIndex, y.TuplePath, y.TupleIndex);

    public int GetHashCode(LocalExpansionKey obj)
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(obj.Value));
        hash.Add(obj.IsInput);
        hash.Add(obj.AccessMode);
        if (obj.TargetProperty is not null) hash.Add(SymbolEqualityComparer.Default.GetHashCode(obj.TargetProperty));
        if (obj.TargetReceiver is not null) hash.Add(SymbolEqualityComparer.Default.GetHashCode(obj.TargetReceiver));
        if (obj.TargetView is not null) hash.Add(SymbolEqualityComparer.Default.GetHashCode(obj.TargetView));
        if (!obj.PropertyChain.IsDefault)
        {
            for (var i = obj.ChainIndex; i < obj.PropertyChain.Length; i++)
                hash.Add(SymbolEqualityComparer.Default.GetHashCode(obj.PropertyChain[i]));
        }
        if (!obj.TuplePath.IsDefault)
        {
            for (var i = obj.TupleIndex; i < obj.TuplePath.Length; i++)
                hash.Add(obj.TuplePath[i]);
        }
        return hash.ToHashCode();
    }

    private static bool SamePropertyChainTail(
        ImmutableArray<IPropertySymbol> left, int leftIndex,
        ImmutableArray<IPropertySymbol> right, int rightIndex)
    {
        var leftLength = left.IsDefault ? 0 : left.Length - leftIndex;
        var rightLength = right.IsDefault ? 0 : right.Length - rightIndex;
        if (leftLength != rightLength) return false;
        for (var i = 0; i < leftLength; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[leftIndex + i], right[rightIndex + i]))
                return false;
        }
        return true;
    }
}

internal readonly record struct LocalExpansionState(
    ProjectionNode Entry,
    ConventionFacts Facts,
    Stack<ProjectionNode> Pending,
    HashSet<LocalExpansionKey> Visited,
    Dictionary<IAnonymousFunctionOperation, SelectorFlowResult>? SelectorScopes);

internal static class TuplePathHelpers
{
    internal static bool SameTail(
        ImmutableArray<int> left, int leftIndex,
        ImmutableArray<int> right, int rightIndex)
    {
        var leftLength = left.IsDefault ? 0 : left.Length - leftIndex;
        var rightLength = right.IsDefault ? 0 : right.Length - rightIndex;
        if (leftLength != rightLength) return false;
        for (var i = 0; i < leftLength; i++)
        {
            if (left[leftIndex + i] != right[rightIndex + i])
                return false;
        }
        return true;
    }

    internal static ImmutableArray<int> Combine(
        ImmutableArray<int> innerPath,
        ImmutableArray<int> outerPath,
        int outerIndex)
    {
        var outerLength = outerPath.IsDefault ? 0 : outerPath.Length - outerIndex;
        if (outerLength <= 0) return innerPath;
        var innerLength = innerPath.IsDefault ? 0 : innerPath.Length;
        if (innerLength + outerLength > ProjectionLimits.MaxTupleDepth) return default;
        if (innerLength == 0)
            return outerIndex == 0 ? outerPath : [.. outerPath.AsSpan(outerIndex, outerLength)];
        var builder = ImmutableArray.CreateBuilder<int>(innerLength + outerLength);
        builder.AddRange(innerPath);
        for (var i = outerIndex; i < outerPath.Length; i++) builder.Add(outerPath[i]);
        return builder.MoveToImmutable();
    }
}
