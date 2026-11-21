using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Domain.SampleIdentity.Users;
using Sample.Domain.SampleIdentity.ProfilePictures;
using SampleUser = Sample.Domain.SampleIdentity.Users.SampleUser;

namespace Sample.Infra.SampleIdentity.Repositories;

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
            {
                var failureDetails = string.Join("; ", claimResult.Errors.Select(e => e.Description));
                var message = string.IsNullOrWhiteSpace(failureDetails)
                    ? "Failed to update the profile-picture version claim."
                    : $"Failed to update the profile-picture version claim: {failureDetails}";
                throw ExceptionFor.Validation(message);
            }

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
