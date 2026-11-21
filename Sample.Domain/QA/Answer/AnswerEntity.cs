using Sample.Domain.QA.Question;
using Sample.Domain.QA.User;
using Sample.Domain.QA.AnswerComment;
using Sample.Contract.QA.Answers;

namespace Sample.Domain.QA.Answer;

[SampleEntityType(SampleEntityTypes.Answer)]
public class AnswerEntity : AggregateRoot
{
    private AnswerEntity()
    {
    }

    public AnswerEntity(string body, QuestionEntity question, UserEntity user)
    {
        Body = body;
        QuestionId = question.Id;
        UserId = user.Id;
    }

    public string Body { get; private set; } = null!;
    public long QuestionId { get; private set; }
    public long UserId { get; private set; }
    public bool IsAccepted { get; set; }

    public List<AnswerCommentEntity> Comments { get; private set; } = [];

    public override AnswerDto ToDto() => new(this)
    {
        Body = Body,
        IsAccepted = IsAccepted
    };
}
