namespace DRN.Framework.SharedKernel.Domain.Repository;

public enum DateTimeFilterType
{
    After = 1,
    Before,
    Between,
    Outside
}

public sealed class EntityCreatedFilter
{
    // Factory methods for better readability and type safety
    public static EntityCreatedFilter After(DateTimeOffset date, bool inclusive = true) =>
        new() { Begin = date, Inclusive = inclusive, Type = DateTimeFilterType.After };

    public static EntityCreatedFilter Before(DateTimeOffset date, bool inclusive = true) =>
        new() { Begin = date, Inclusive = inclusive, Type = DateTimeFilterType.Before };

    public static EntityCreatedFilter Between(DateTimeOffset begin, DateTimeOffset end, bool inclusive = true) =>
        new() { Begin = begin, End = end, Inclusive = inclusive, Type = DateTimeFilterType.Between };

    public static EntityCreatedFilter Outside(DateTimeOffset begin, DateTimeOffset end, bool inclusive = true) =>
        new() { Begin = begin, End = end, Inclusive = inclusive, Type = DateTimeFilterType.Outside };

    public DateTimeFilterType Type { get; private set; }
    public DateTimeOffset Begin { get; private set; }
    public DateTimeOffset? End { get; private set; }
    public bool Inclusive { get; private set; }
}