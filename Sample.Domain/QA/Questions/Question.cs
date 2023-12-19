using Sample.Domain.QA.Answers;
using Sample.Domain.QA.Comments;
using Sample.Domain.QA.Tags;

namespace Sample.Domain.QA.Questions;

public class Question
{
    public Question(string title, string body)
    {
        Title = title;
        Body = body;
    }

    public int Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public int PostedBy { get; set; }
    public int Category { get; set; }
    public DateTimeOffset DatePosted { get; set; }
    public List<Tag> Tags { get; } = new();
    public List<Answer> Answers { get; set; }
    public List<Comment> Comments { get; set; }
}