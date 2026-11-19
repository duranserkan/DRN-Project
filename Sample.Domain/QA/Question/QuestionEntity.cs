using Sample.Domain.QA.Answer;
using Sample.Domain.QA.Category;
using Sample.Domain.QA.Tag;
using Sample.Domain.QA.User;
using Sample.Domain.QA.QuestionComment;
using Sample.Contract.QA.Questions;

namespace Sample.Domain.QA.Question;

[SampleEntityType(SampleEntityTypes.Question)]
public class QuestionEntity : AggregateRoot
{
    private QuestionEntity()
    {
    }

    public QuestionEntity(string title, string body, UserEntity user, CategoryEntity category)
    {
        Title = title;
        Body = body;
        User = user;
        UserId = user.Id;
        Category = category;
        CategoryId = category.Id;
    }

    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    public long UserId { get; private set; }
    public UserEntity User { get; private set; } = null!;
    public long CategoryId { get; private set; }
    public CategoryEntity Category { get; private set; } = null!;

    public List<TagEntity> Tags { get; private set; } = [];
    public List<AnswerEntity> Answers { get; private set; } = [];
    public List<QuestionCommentEntity> Comments { get; private set; } = [];

    public QuestionDto ToDto() => new(this)
    {
        Title = Title,
        Body = Body
    };
}
