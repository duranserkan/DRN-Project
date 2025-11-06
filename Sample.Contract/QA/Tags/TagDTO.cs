using System.Text.Json.Serialization;
using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Tags;

public class TagDto : Dto
{
    [JsonConstructor]
    private TagDto()
    {
    }

    public TagDto(SourceKnownEntity entity) : base(entity)
    {
    }

    public required string Name { get; init; } = string.Empty;
    public required TagValueModel Model { get; init; } = new();
}