using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Authors;

public class Author : AggregateRoot
{
    private Author()
    {
    }

    public Author(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public List<Question> Posts { get; private set; } = [];

    protected override EntityCreated? GetCreatedEvent() => null;
    protected override EntityModified? GetModifiedEvent() => null;
    protected override EntityDeleted? GetDeletedEvent() => null;
}