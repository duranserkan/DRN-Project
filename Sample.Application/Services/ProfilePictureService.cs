using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity.ProfilePictures;
using Sample.Utils.Image;

namespace Sample.Application.Services;

public interface IProfilePictureService
{
    Task CreateProfilePictureAsync(IdentityUser user, Stream pictureStream, long maxSize);
    Task<string?> GetProfilePictureAsBase64Async(IdentityUser user);
}

[Transient<IProfilePictureService>]
public class ProfilePictureService(IJpegUtils jpegUtils, IProfilePictureRepository repository) : IProfilePictureService
{
    public async Task CreateProfilePictureAsync(IdentityUser user, Stream pictureStream, long maxSize)
    {
        var pictureBytes = pictureStream.ToByteArray(maxSize);
        var profilePicture = new ProfilePicture(user, pictureBytes);

        await repository.UpdateProfilePictureAsync(profilePicture);
    }

    public async Task<string?> GetProfilePictureAsBase64Async(IdentityUser user)
    {
        var profilePicture = await repository.GetProfilePictureAsync(user);
        if (profilePicture == null) return null;

        var base64String = Convert.ToBase64String(profilePicture.ImageData);

        return base64String;
    }
}