using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Question;

namespace Sample.Infra.QA.Configurations;

public class QuestionConfig : IEntityTypeConfiguration<QuestionEntity>
{
    public void Configure(EntityTypeBuilder<QuestionEntity> builder)
    {
        builder
            .HasOne(question => question.User)
            .WithMany()
            .IsRequired();

        builder.HasOne(question => question.Category)
            .WithMany(category => category.Questions)
            .HasForeignKey(question => question.CategoryId);

        builder.HasQueryFilter(question => true);
        builder.HasMany(question => question.Answers).WithOne().HasForeignKey(answer => answer.QuestionId);
        builder.HasMany(question => question.Comments).WithOne().HasForeignKey("QuestionId");
    }
}
