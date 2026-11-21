using System.Text.Json.Serialization;

namespace Sample.Contract.QA.Tags;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class TagPostRequest
{
    public string Name { get; init; } = string.Empty;
    public TagModelPostRequest Model { get; init; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class TagModelPostRequest
{
    public bool BoolValue { get; init; }
    public string StringValue { get; init; } = string.Empty;
    public long Max { get; init; }
    public long Min { get; init; }
    public long Other { get; init; }
    public DateTimeOffset Date { get; init; } = DateTimeOffset.UnixEpoch;
    public TagType? Type { get; init; }
}
