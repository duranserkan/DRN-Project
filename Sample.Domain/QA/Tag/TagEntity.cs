using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Question;

namespace Sample.Domain.QA.Tag;

[SampleEntityType(SampleEntityTypes.Tag)]
public class TagEntity : AggregateRoot<TagValueModel>
{
    private TagEntity()
    {
    }

    public TagEntity(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public List<QuestionEntity> Questions { get; private set; } = [];

    public TagDto ToDto() => new(this)
    {
        Name = Name,
        Model = Model
    };
}
