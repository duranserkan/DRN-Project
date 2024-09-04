using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Identity.ProfilePictures;

public class ProfilePicture
{
    private ProfilePicture()
    {
    }

    public ProfilePicture(IdentityUser user, byte[] imageData)
    {
        UserId = user.Id;
        ImageData = imageData;
    }

    public long Id { get; private set; }
    public string UserId { get; private set; }
    public byte[] ImageData { get; private set; } = null!;

    public void UpdateImageData(byte[] imageData)
    {
        ImageData = imageData;
    }
}