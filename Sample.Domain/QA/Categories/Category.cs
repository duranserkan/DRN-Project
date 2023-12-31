using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Categories;

public class Category : AggregateRoot
{
    private Category()
    {
    }

    public Category(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public List<Question> Questions { get; private set; } = [];
}