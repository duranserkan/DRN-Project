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

    public string Name { get; set; }
    public List<Question> Posts { get; } = new();
}