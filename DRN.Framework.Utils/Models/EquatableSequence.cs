using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace DRN.Framework.Utils.Models;

public static class EquatableSequenceBuilder
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableSequence<T> Create<T>(ReadOnlySpan<T> values) where T : notnull
        => new(values.ToArray());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableImmutableSequence<T> CreateImmutable<T>(ReadOnlySpan<T> values) where T : notnull
        => new(ImmutableArray.Create(values));
}

/// <summary>
/// A lightweight, readonly record struct wrapper around a mutable array (<typeparamref name="T"/>[]) providing SIMD-accelerated sequence equality and structural hashing.
/// <para>
/// <b>When to prefer:</b> Preferred in performance-critical, transient lookups and cache keys (e.g. <c>MethodCacheKey</c> in reflection invokers) where zero heap allocations on key construction and direct native array interoperability are required.
/// </para>
/// </summary>
[CollectionBuilder(typeof(EquatableSequenceBuilder), nameof(EquatableSequenceBuilder.Create))]
public readonly record struct EquatableSequence<T>(T[]? Items) : IReadOnlyList<T> where T : notnull
{
    public int Count => Items?.Length ?? 0;

    public T this[int index] => Items is not null ? Items[index] : throw new ArgumentOutOfRangeException(nameof(index));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(EquatableSequence<T> other) => ReferenceEquals(Items, other.Items) || Items.AsSpan().SequenceEqual(other.Items.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (Items is null || Items.Length == 0)
            return 0;

        var hc = new HashCode();
        foreach (ref readonly var item in Items.AsSpan())
        {
            hc.Add(item);
        }

        return hc.ToHashCode();
    }


    public Span<T>.Enumerator GetEnumerator() => Items.AsSpan().GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>?)Items ?? Array.Empty<T>()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable?)Items ?? Array.Empty<T>()).GetEnumerator();

    public static implicit operator EquatableSequence<T>(T[]? items) => new(items);
}

/// <summary>
/// A lightweight, readonly record struct wrapper around an <see cref="ImmutableArray{T}"/> providing SIMD-accelerated sequence equality and structural hashing with deep immutability guarantees.
/// <para>
/// <b>When to prefer:</b> Preferred in long-lived domain models, value objects, records, and public API surfaces where the underlying collection must be guaranteed immutable and shielded against external buffer mutation after creation.
/// </para>
/// </summary>
[CollectionBuilder(typeof(EquatableSequenceBuilder), nameof(EquatableSequenceBuilder.CreateImmutable))]
public readonly record struct EquatableImmutableSequence<T>(ImmutableArray<T> Items) : IReadOnlyList<T> where T : notnull
{
    public int Count => Items.IsDefault ? 0 : Items.Length;

    public T this[int index] => !Items.IsDefault ? Items[index] : throw new ArgumentOutOfRangeException(nameof(index));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(EquatableImmutableSequence<T> other)
    {
        if (Items.Equals(other.Items))
            return true;

        var span1 = Items.IsDefault ? ReadOnlySpan<T>.Empty : Items.AsSpan();
        var span2 = other.Items.IsDefault ? ReadOnlySpan<T>.Empty : other.Items.AsSpan();
        return span1.SequenceEqual(span2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (Items.IsDefaultOrEmpty)
            return 0;

        var hc = new HashCode();
        foreach (ref readonly var item in Items.AsSpan())
        {
            hc.Add(item);
        }

        return hc.ToHashCode();
    }


    public ImmutableArray<T>.Enumerator GetEnumerator() => (Items.IsDefault ? ImmutableArray<T>.Empty : Items).GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)(Items.IsDefault ? ImmutableArray<T>.Empty : Items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)(Items.IsDefault ? ImmutableArray<T>.Empty : Items)).GetEnumerator();

    public static implicit operator EquatableImmutableSequence<T>(ImmutableArray<T> items) => new(items);
}
