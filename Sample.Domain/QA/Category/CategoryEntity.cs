using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Question;

namespace Sample.Domain.QA.Category;

[SampleEntityType(SampleEntityTypes.Category)]
public class CategoryEntity : AggregateRoot
{
    private CategoryEntity()
    {
    }

    public CategoryEntity(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public List<QuestionEntity> Questions { get; private set; } = [];

    public override CategoryDto ToDto() => new(this)
    {
        Name = Name
    };
}
