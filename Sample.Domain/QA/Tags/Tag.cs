using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Tags;

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Question> Posts { get; } = new();
}