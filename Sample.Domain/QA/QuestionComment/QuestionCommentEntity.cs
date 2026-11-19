using Sample.Domain.QA.User;
using Sample.Contract.QA.QuestionComments;

namespace Sample.Domain.QA.QuestionComment;

[SampleEntityType(SampleEntityTypes.QuestionComment)]
public class QuestionCommentEntity : AggregateRoot
{
    private QuestionCommentEntity()
    {
    }

    public QuestionCommentEntity(string body, UserEntity user)
    {
        Body = body;
        UserId = user.Id;
    }

    public string Body { get; private set; } = null!;
    public long UserId { get; private set; }

    public QuestionCommentDto ToDto() => new(this)
    {
        Body = Body
    };
}
