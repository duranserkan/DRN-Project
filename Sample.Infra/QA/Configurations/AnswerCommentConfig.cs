using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.AnswerComment;

namespace Sample.Infra.QA.Configurations;

public class AnswerCommentConfig : IEntityTypeConfiguration<AnswerCommentEntity>
{
    public void Configure(EntityTypeBuilder<AnswerCommentEntity> builder)
    {
        builder.ToTable("answer_comment");
        builder.HasMany(comment => comment.Comments).WithOne().HasForeignKey("AnswerCommentId");
    }
}
