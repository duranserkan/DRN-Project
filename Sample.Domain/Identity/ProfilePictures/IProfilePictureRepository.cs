using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Identity.ProfilePictures;

public interface IProfilePictureRepository
{
    public Task UpdateProfilePictureAsync(ProfilePicture picture);

    public Task<ProfilePicture?> GetProfilePictureAsync(IdentityUser user);
}