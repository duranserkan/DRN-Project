using System.Text.Json.Serialization;

namespace Sample.Contract.QA.Categories;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CategoryPostRequest
{
    public string Name { get; init; } = string.Empty;
}
