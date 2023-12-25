using Sample.Domain.Users;

namespace Sample.Domain.QA.Questions;

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

    public string Body { get; set; }
    public long UserId { get; set; }
}