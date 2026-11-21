namespace Sample.Contract.QA.Tags;

public class TagPostRequest
{
    public string Name { get; init; } = string.Empty;
    public TagValueModel Model { get; init; } = new();
}