using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Questions;

namespace Sample.Infra.QA.Configurations;

public class QuestionCommentConfig : IEntityTypeConfiguration<QuestionComment>
{
    public void Configure(EntityTypeBuilder<QuestionComment> builder)
    {
    }
}