namespace Sample.Contract.QA.Tags;

public sealed class TagResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public required string Name { get; init; }
    public required TagModelResponse Model { get; init; }
}

public sealed class TagModelResponse
{
    public bool BoolValue { get; init; }
    public string StringValue { get; init; } = string.Empty;
    public long Max { get; init; }
    public long Min { get; init; }
    public long Other { get; init; }
    public DateTimeOffset Date { get; init; } = DateTimeOffset.UnixEpoch;
    public TagType Type { get; init; }
}
