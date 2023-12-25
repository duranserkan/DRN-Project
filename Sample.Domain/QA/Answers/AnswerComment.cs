using Sample.Domain.Users;

namespace Sample.Domain.QA.Answers;

public class AnswerComment : AggregateRoot
{
    private AnswerComment()
    {
    }

    public AnswerComment(string body, Answer answer, User user)
    {
        Body = body;
        AnswerId = answer.Id;
        UserId = user.Id;
    }

    public string Body { get; set; }
    public long UserId { get; private set; }
    public long AnswerId { get; private set; }

    public List<AnswerComment> Comments { get; set; } = new();
}