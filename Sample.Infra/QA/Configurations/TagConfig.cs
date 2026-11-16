using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.Tag;

namespace Sample.Infra.QA.Configurations;

public class TagConfig : IEntityTypeConfiguration<TagEntity>
{
    public void Configure(EntityTypeBuilder<TagEntity> builder)
    {
        builder
            .HasMany(tag => tag.Questions)
            .WithMany(question => question.Tags)
            .UsingEntity("QuestionTag");

        builder.Navigation(tag => tag.Questions).AutoInclude();
    }
}
