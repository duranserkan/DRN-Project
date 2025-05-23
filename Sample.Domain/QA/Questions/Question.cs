using Sample.Domain.QA.Answers;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Tags;
using Sample.Domain.Users;

namespace Sample.Domain.QA.Questions;

[EntityTypeId((int)SampleEntityTypeIds.Question)]
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

    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    public long UserId { get; private set; }
    public User User { get; private set; } = null!;
    public long CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public List<Tag> Tags { get; private set; } = [];
    public List<Answer> Answers { get; private set; } = [];
    public List<QuestionComment> Comments { get; private set; } = [];
}