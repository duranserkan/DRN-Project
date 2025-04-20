using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Questions;

namespace Sample.Infra.QA.Configurations;

public class QuestionConfig : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder
            .HasOne(question => question.User)
            .WithMany()
            .IsRequired();

        builder.HasOne(question => question.Category)
            .WithMany(category => category.Questions)
            .HasForeignKey(question => question.CategoryId);

        builder.HasQueryFilter(question => true);
    }
}