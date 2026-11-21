
using Sample.Domain.SampleIdentity.Users;

namespace Sample.Domain.SampleIdentity.ProfilePictures;

public interface IProfilePictureRepository
{
    public Task UpdateProfilePictureAsync(ProfilePicture picture, SampleUser user);

    public Task<ProfilePicture?> GetProfilePictureAsync(string userId);
}
