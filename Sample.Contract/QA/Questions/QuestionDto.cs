using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.Questions;

public class QuestionDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Title { get; init; } = string.Empty;
    public required string Body { get; init; } = string.Empty;
}
