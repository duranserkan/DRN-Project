using DRN.Framework.SharedKernel.Domain;

namespace Sample.Contract.QA.QuestionComments;

public class QuestionCommentDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public required string Body { get; init; } = string.Empty;
}
