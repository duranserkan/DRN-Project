using DRN.Framework.SharedKernel.Attributes;

namespace Sample.Contract.QA.Tags;

public class TagValueModel
{
    public bool BoolValue { get; set; }
    public string StringValue { get; set; } = string.Empty;
    public long Max { get; set; }
    public long Min { get; set; }
    public long Other { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UnixEpoch;

    public TagType Type { get; set; }
}

[AllowZero]
public enum TagType
{
    System = 0,
    User,
}