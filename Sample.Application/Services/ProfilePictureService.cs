using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Domain.Users;

namespace Sample.Application.Services;

public interface IProfilePictureService
{
    Task CreateProfilePictureAsync(SampleUser user, Stream pictureStream, long maxSize);
}

[Transient<IProfilePictureService>]
public class ProfilePictureService(IProfilePictureRepository repository) : IProfilePictureService
{
    public async Task CreateProfilePictureAsync(SampleUser user, Stream pictureStream, long maxSize)
    {
        var pictureBytes = pictureStream.ToByteArray(maxSize);
        var profilePicture = new ProfilePicture(user, pictureBytes);

        await repository.UpdateProfilePictureAsync(profilePicture, user);
    }
}