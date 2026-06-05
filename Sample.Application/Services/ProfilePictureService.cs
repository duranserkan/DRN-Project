using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;
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
        var pictureBytes = await pictureStream.ToArrayAsync(maxSize);
        if (!IsJpeg(pictureBytes))
            throw ExceptionFor.Validation("Profile picture must be a valid JPEG image.");

        var profilePicture = new ProfilePicture(user, pictureBytes);

        await repository.UpdateProfilePictureAsync(profilePicture, user);
    }

    private static bool IsJpeg(ReadOnlySpan<byte> imageData)
        => imageData.Length >= 4
           && imageData[0] == 0xFF
           && imageData[1] == 0xD8
           && imageData[2] == 0xFF
           && imageData[^2] == 0xFF
           && imageData[^1] == 0xD9;
}
