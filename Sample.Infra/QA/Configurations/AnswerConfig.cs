using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Answer;

namespace Sample.Infra.QA.Configurations;

public class AnswerConfig : IEntityTypeConfiguration<AnswerEntity>
{
    public void Configure(EntityTypeBuilder<AnswerEntity> builder)
    {
        builder.HasMany(answer => answer.Comments).WithOne().HasForeignKey(comment => comment.AnswerId);
    }
}
