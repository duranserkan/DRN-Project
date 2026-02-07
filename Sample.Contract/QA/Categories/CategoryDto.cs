using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Categories;

public class CategoryDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Name { get; init; } = string.Empty;
}
