using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity.ProfilePictures;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IProfilePictureRepository>]
public class ProfilePictureRepository(SampleIdentityContext context) : IProfilePictureRepository
{
    public async Task UpdateProfilePictureAsync(ProfilePicture picture)
    {
        var existingProfilePicture = context.ProfilePictures.FirstOrDefault(p => p.UserId == picture.UserId);

        if (existingProfilePicture != null)
            existingProfilePicture.UpdateImageData(picture.ImageData);
        else
            context.ProfilePictures.Add(picture);

        await context.SaveChangesAsync();
    }

    public async Task<ProfilePicture?> GetProfilePictureAsync(IdentityUser user)
    {
        var existingProfilePicture = await context.ProfilePictures.FirstOrDefaultAsync(p => p.UserId == user.Id);

        return existingProfilePicture;
    }
}