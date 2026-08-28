using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Domain.Identity;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IProfilePictureRepository>]
public class ProfilePictureRepository(SampleIdentityContext context, IUserClaimRepository claimRepository) : IProfilePictureRepository
{
    public async Task UpdateProfilePictureAsync(ProfilePicture picture, SampleUser user)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var existingProfilePicture = await context.ProfilePictures.FirstOrDefaultAsync(p => p.UserId == picture.UserId);

            if (existingProfilePicture != null)
                existingProfilePicture.UpdateImageData(picture.ImageData);
            else
                context.ProfilePictures.Add(picture);

            await context.SaveChangesAsync();

            var claimResult = await claimRepository.UpdateProfilePictureVersionClaimAsync(user, existingProfilePicture?.Version ?? picture.Version);
            if (!claimResult.Succeeded)
                throw new InvalidOperationException("Failed to update the profile-picture version claim.");

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
