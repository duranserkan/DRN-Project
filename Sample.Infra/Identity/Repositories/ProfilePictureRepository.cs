using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;
using Sample.Domain.Identity.ProfilePictures;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IProfilePictureRepository>]
public class ProfilePictureRepository(SampleIdentityContext context, IUserClaimRepository claimRepository) : IProfilePictureRepository
{
    public async Task UpdateProfilePictureAsync(ProfilePicture picture, IdentityUser user)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var existingProfilePicture = await context.ProfilePictures.FirstOrDefaultAsync(p => p.UserId == picture.UserId);

            if (existingProfilePicture != null)
                existingProfilePicture.UpdateImageData(picture.ImageData);
            else
                context.ProfilePictures.Add(picture);

            await claimRepository.UpdateProfilePictureVersionClaimAsync(user, existingProfilePicture?.Version ?? picture.Version);
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ProfilePicture?> GetProfilePictureAsync(string userId)
    {
        var existingProfilePicture = await context.ProfilePictures.FirstOrDefaultAsync(p => p.UserId == userId);

        return existingProfilePicture;
    }
}