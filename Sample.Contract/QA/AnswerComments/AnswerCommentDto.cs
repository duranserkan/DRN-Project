using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.AnswerComments;

public class AnswerCommentDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Body { get; init; } = string.Empty;
}
