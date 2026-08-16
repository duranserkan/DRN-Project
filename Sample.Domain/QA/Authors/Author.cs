using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Authors;

[SampleEntityType(SampleEntityTypes.Author)]
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
}