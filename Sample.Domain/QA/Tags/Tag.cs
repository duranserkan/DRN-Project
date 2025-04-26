using Sample.Domain.QA.Questions;

namespace Sample.Domain.QA.Tags;

[EntityTypeId((int)SampleEntityTypeIds.Tag)]
public class Tag : Entity<TagValueModel>
{
    private Tag()
    {
    }

    public Tag(string name)
    {
        Name = name;
    }

    public string Name { get; private set; } = null!;
    public List<Question> Questions { get; private set; } = [];
}

public class TagValueModel
{
    public bool BoolValue { get; set; }
    public string StringValue { get; set; } = string.Empty;
}