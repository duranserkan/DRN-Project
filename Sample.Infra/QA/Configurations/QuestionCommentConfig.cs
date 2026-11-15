using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.QuestionComment;

namespace Sample.Infra.QA.Configurations;

public class QuestionCommentConfig : IEntityTypeConfiguration<QuestionCommentEntity>
{
    public void Configure(EntityTypeBuilder<QuestionCommentEntity> builder)
    {
    }
}
