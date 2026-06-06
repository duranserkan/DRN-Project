using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Validators;
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
    private const string InvalidJpegMessage = "Profile picture must be a valid JPEG image.";
    private const string MaxSizeExceededMessage = "Profile picture exceeds the maximum allowed size.";

    public async Task CreateProfilePictureAsync(SampleUser user, Stream pictureStream, long maxSize)
    {
        var validation = await JpegValidator.ValidateAsync(pictureStream, maxSize);
        if (!validation.IsValid)
            throw ExceptionFor.Validation(validation.ErrorReason == JpegValidationErrorReason.MaxLengthExceeded
                ? MaxSizeExceededMessage
                : InvalidJpegMessage);

        var profilePicture = new ProfilePicture(user, validation.ImageData);

        await repository.UpdateProfilePictureAsync(profilePicture, user);
    }
}
