using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Tests.Sample.Infra.QA.Repositories.Data;

public static class TagGenerator
{
    public static Tag New(string prefix, string suffix, bool value = true) => new($"{prefix}_{suffix}")
    {
        Model = new TagValueModel
        {
            BoolValue = value,
            StringValue = $"{suffix}Value",
            Max = long.MaxValue,
            Min = long.MinValue,
            Other = 0,
            Type = TagType.System
        }
    };

    public static (Tag firstTag, Tag secondTag, Tag thirdTag) GetTags(string tagPrefix)
    {
        var firstTag = New(tagPrefix, "firstTag");
        var secondTag = New(tagPrefix, "secondTag", false);
        var thirdTag = New(tagPrefix, "thirdTag");

        return (firstTag, secondTag, thirdTag);
    }
}