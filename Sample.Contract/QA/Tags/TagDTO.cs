using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Tags;

public class TagDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Name { get; init; } = string.Empty;
    public required TagValueModel Model { get; init; } = new();
}