using DRN.Framework.SharedKernel.Attributes;
using Sample.Domain.QA.Question;
using Sample.Contract.QA.Authors;

namespace Sample.Domain.QA.Author;

[SampleEntityType(SampleEntityTypes.Author)]
public class AuthorEntity : AggregateRoot
{
    private AuthorEntity()
    {
    }

    public AuthorEntity(string name)
    {
        Name = name;
    }

    [Personal]
    public string Name { get; private set; } = null!;

    public List<QuestionEntity> Posts { get; private set; } = [];

    public AuthorDto ToDto() => new(this);
}
