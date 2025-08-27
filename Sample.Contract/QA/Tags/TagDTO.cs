namespace Sample.Contract.QA.Tags;

public class TagDto
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ModifiedAt { get; init; }
    public required string Name { get; init; } = string.Empty;
    public required TagValueModel Model { get; init; } = new();
}