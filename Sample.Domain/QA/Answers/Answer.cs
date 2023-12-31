using Sample.Domain.QA.Questions;
using Sample.Domain.Users;

namespace Sample.Domain.QA.Answers;

public class Answer : AggregateRoot
{
    private Answer()
    {
    }

    public Answer(string body, Question question, User user)
    {
        Body = body;
        QuestionId = question.Id;
        UserId = user.Id;
    }

    public string Body { get; private set; } = null!;
    public long QuestionId { get; private set; }
    public long UserId { get; private set; }
    public bool IsAccepted { get; set; }

    public List<AnswerComment> Comments { get; private set; } = [];

    protected override EntityCreated? GetCreatedEvent() => null;
    protected override EntityModified? GetModifiedEvent() => null;
    protected override EntityDeleted? GetDeletedEvent() => null;
}