using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Tags;

namespace Sample.Infra.QA.Configurations;

public class TagConfig : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder
            .HasMany(tag => tag.Questions)
            .WithMany(question => question.Tags);
    }
}