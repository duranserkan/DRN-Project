namespace Sample.Contract.QA.Categories;

public sealed class CategoryResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public required string Name { get; init; }
}
