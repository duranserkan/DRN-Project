using Sample.Domain.Users;

namespace Sample.Domain.QA.Questions;

[EntityType((int)SampleEntityTypes.QuestionComment)]
public class QuestionComment : AggregateRoot
{
    private QuestionComment()
    {
    }

    public QuestionComment(string body, User user)
    {
        Body = body;
        UserId = user.Id;
    }

    public string Body { get; private set; } = null!;
    public long UserId { get; private set; }
}