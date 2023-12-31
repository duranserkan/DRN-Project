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

    public string Name { get; private set; } = null!;
    public List<Question> Questions { get; private set; } = [];
}