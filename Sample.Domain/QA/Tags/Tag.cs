using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Tags;

public class Tag : Entity
{
    private Tag()
    {
    }

    public Tag(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public List<Question> Posts { get; } = new();
}