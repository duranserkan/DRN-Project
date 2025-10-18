using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Tags;

[EntityType((int)SampleEntityTypes.Tag)]
public class Tag : AggregateRoot<TagValueModel>
{
    private Tag()
    {
    }

    public Tag(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public string? Value { get; private set; }
    public List<Question> Questions { get; private set; } = [];

    public TagDto ToDto() => new()
    {
        Id = EntityId,
        CreatedAt = CreatedAt,
        ModifiedAt = ModifiedAt,
        Name = Name,
        Model = Model
    };
}