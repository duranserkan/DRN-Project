using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QB.QBTests;

public class QBTestDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required long TestValue { get; init; }
    public required string TestValueString { get; init; } = string.Empty;
}
