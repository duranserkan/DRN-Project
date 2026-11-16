using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.SampleIdentity.ProfilePictures;
using SampleUser = Sample.Domain.SampleIdentity.SampleUser;

namespace Sample.Infra.SampleIdentity.Configurations;

public class ProfilePictureConfig : IEntityTypeConfiguration<ProfilePicture>
{
    public void Configure(EntityTypeBuilder<ProfilePicture> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.ImageData)
            .IsRequired();

        builder.Property(p => p.Version);

        builder.HasOne<SampleUser>()
            .WithOne()
            .HasForeignKey<ProfilePicture>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
