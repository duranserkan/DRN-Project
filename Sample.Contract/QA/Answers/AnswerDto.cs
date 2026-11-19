using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Answers;

public class AnswerDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Body { get; init; } = string.Empty;
    public required bool IsAccepted { get; init; }
}
