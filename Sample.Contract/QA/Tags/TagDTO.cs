namespace Sample.Contract.QA.Tags;

public class TagDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public TagValueModel Model { get; init; } = new();
}