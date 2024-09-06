using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Identity.ProfilePictures;

public interface IProfilePictureRepository
{
    public Task UpdateProfilePictureAsync(ProfilePicture picture, IdentityUser user);

    public Task<ProfilePicture?> GetProfilePictureAsync(string userId);
}