using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Questions;

namespace Sample.Infra.Repositories.QA.Configurations;

public class QuestionConfig : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable(DbConstants.Questions, DbConstants.Schema);
        builder.HasKey(question => question.Id);

        builder.HasQueryFilter(question => true);
    }
}