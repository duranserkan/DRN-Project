using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Category;

namespace Sample.Infra.QA.Configurations;

public class CategoryConfig : IEntityTypeConfiguration<CategoryEntity>
{
    public void Configure(EntityTypeBuilder<CategoryEntity> builder)
    {
        builder
            .HasMany(category => category.Questions)
            .WithOne(question => question.Category)
            .HasForeignKey(question => question.CategoryId)
            .IsRequired();
    }
}
