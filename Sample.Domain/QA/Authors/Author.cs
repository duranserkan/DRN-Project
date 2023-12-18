using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Authors;

public class Author
{
    public Author(string name)
    {
        Name = name;
    }

    public int Id { get; private set; }
    public string Name { get; set; }
    public List<Question> Posts { get; } = new();
}