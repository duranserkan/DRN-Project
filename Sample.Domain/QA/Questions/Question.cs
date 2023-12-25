using Sample.Domain.QA.Answers;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Tags;
using Sample.Domain.Users;

namespace Sample.Domain.QA.Questions;

public class Question : AggregateRoot
{
    private Question()
    {
    }

    public Question(string title, string body, User user, Category category)
    {
        Title = title;
        Body = body;
        UserId = user.Id;
        CategoryId = category.Id;
    }

    public string Title { get; set; }
    public string Body { get; set; }

    public long UserId { get; set; }
    public User User { get; set; }
    public long CategoryId { get; set; }
    public Category Category { get; set; }

    public List<Tag> Tags { get; } = new();
    public List<Answer> Answers { get; set; } = new();
    public List<QuestionComment> Comments { get; set; } = new();
}