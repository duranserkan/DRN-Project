using System.Runtime.CompilerServices;

namespace DRN.Framework.Utils.Models;

public readonly record struct EquatableSequence<T>(T[] Items) : IEquatable<EquatableSequence<T>> where T : notnull
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(EquatableSequence<T> other) =>
        ReferenceEquals(Items, other.Items) ||
        (Items is not null && other.Items is not null && Items.AsSpan().SequenceEqual(other.Items));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (Items is null || Items.Length == 0)
            return 0;

        if (Items.Length == 1)
            return Items[0].GetHashCode();

        var hc = new HashCode();
        foreach (var item in Items)
            hc.Add(item);
        return hc.ToHashCode();
    }
}