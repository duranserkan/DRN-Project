using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Categories;

namespace Sample.Infra.QA.Configurations;

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable(TableContants.Categories, DbConstants.QASchema);
        builder
            .HasMany(category => category.Questions)
            .WithOne(question => question.Category)
            .HasForeignKey(question => question.CategoryId)
            .IsRequired();
    }
}