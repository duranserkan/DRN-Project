using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Integration.Tests.Sample.Infra.QA.Repositories.Data;

public static class TagGenerator
{
    public static Tag New(string prefix, string suffix, bool value = true, long other = 0) => new($"{prefix}_{suffix}")
    {
        Model = new TagValueModel
        {
            BoolValue = value,
            StringValue = $"{suffix}Value",
            Max = long.MaxValue,
            Min = long.MinValue,
            Other = other,
            Type = TagType.System
        }
    };

    public static (Tag firstTag, Tag secondTag, Tag thirdTag) GetTags(string tagPrefix)
    {
        var firstTag = New(tagPrefix, "firstTag", other: long.MaxValue - 1);
        var secondTag = New(tagPrefix, "secondTag", false, other: long.MaxValue - 2);
        var thirdTag = New(tagPrefix, "thirdTag", other: long.MaxValue - 3);

        return (firstTag, secondTag, thirdTag);
    }
}