namespace DRN.Framework.Utils.Entity;

public enum DateTimeFilterType
{
    After = 1,
    Before,
    Between,
    Outside
}

public sealed class EntityCreated
{
    // Factory methods for better readability and type safety
    public static EntityCreated After(DateTimeOffset date, bool inclusive = true) =>
        new() { Begin = date, Inclusive = inclusive, Type = DateTimeFilterType.After };

    public static EntityCreated Before(DateTimeOffset date, bool inclusive = true) =>
        new() { Begin = date, Inclusive = inclusive, Type = DateTimeFilterType.Before };

    public static EntityCreated Between(DateTimeOffset begin, DateTimeOffset end, bool inclusive = true) =>
        new() { Begin = begin, End = end, Inclusive = inclusive, Type = DateTimeFilterType.Between };

    public static EntityCreated Outside(DateTimeOffset begin, DateTimeOffset end, bool inclusive = true) =>
        new() { Begin = begin, End = end, Inclusive = inclusive, Type = DateTimeFilterType.Outside };

    public DateTimeFilterType Type { get; private set; }
    public DateTimeOffset Begin { get; private set; }
    public DateTimeOffset? End { get; private set; }
    public bool Inclusive { get; private set; }
}