using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;

namespace Sample.Infra.Identity.Configurations;

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