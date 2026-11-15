using Sample.Domain.QA.User;
using Sample.Domain.QA.Answer;

namespace Sample.Domain.QA.AnswerComment;

[SampleEntityType(SampleEntityTypes.AnswerComment)]
public class AnswerCommentEntity : AggregateRoot
{
    private AnswerCommentEntity()
    {
    }

    public AnswerCommentEntity(string body, AnswerEntity answer, UserEntity user)
    {
        Body = body;
        AnswerId = answer.Id;
        UserId = user.Id;
    }

    public string Body { get; private set; } = null!;
    public long UserId { get; private set; }
    public long AnswerId { get; private set; }

    public List<AnswerCommentEntity> Comments { get; private set; } = [];

}
