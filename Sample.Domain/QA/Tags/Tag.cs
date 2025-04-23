using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Tags;

[EntityTypeId((int)SampleEntityTypeIds.Tag)]
public class Tag : Entity
{
    private Tag()
    {
    }

    public Tag(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public List<Question> Questions { get; private set; } = [];

    protected override EntityCreated? GetCreatedEvent() => null;
    protected override EntityModified? GetModifiedEvent() => null;
    protected override EntityDeleted? GetDeletedEvent() => null;
}