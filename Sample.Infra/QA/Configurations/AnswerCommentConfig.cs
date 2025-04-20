using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Answers;

namespace Sample.Infra.QA.Configurations;

public class AnswerCommentConfig : IEntityTypeConfiguration<AnswerComment>
{
    public void Configure(EntityTypeBuilder<AnswerComment> builder)
    {
    }
}