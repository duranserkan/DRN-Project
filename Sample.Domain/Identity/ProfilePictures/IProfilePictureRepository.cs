using Sample.Domain.Users;

namespace Sample.Domain.Identity.ProfilePictures;

public interface IProfilePictureRepository
{
    public Task UpdateProfilePictureAsync(ProfilePicture picture, SampleUser user);

    public Task<ProfilePicture?> GetProfilePictureAsync(string userId);
}