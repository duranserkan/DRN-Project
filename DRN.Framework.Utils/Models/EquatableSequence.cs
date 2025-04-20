namespace DRN.Framework.Utils.Models;

public sealed record EquatableSequence<T>(T[] Items) where T : notnull
{
    public bool Equals(EquatableSequence<T>? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode()
    {
        if (Items.Length == 1)
            return Items[0].GetHashCode();

        var hc = new HashCode();
        foreach (var item in Items)
            hc.Add(item);
        return hc.ToHashCode();
    }
}