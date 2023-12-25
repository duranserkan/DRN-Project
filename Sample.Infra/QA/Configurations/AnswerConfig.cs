using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Answers;

namespace Sample.Infra.QA.Configurations;

public class AnswerConfig : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable(TableContants.Answers, DbConstants.QASchema);
    }
}