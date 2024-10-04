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
        Version = 1;
    }

    public long Id { get; init; }
    public string UserId { get; init; } = string.Empty;
    public byte[] ImageData { get; private set; } = null!;
    public byte Version { get; private set; }

    public void UpdateImageData(byte[] imageData)
    {
        ImageData = imageData;
        IncrementVersion();
    }

    private void IncrementVersion()
    {
        // Increment the version, with handling to avoid overflow of the byte value
        Version = (byte)((Version + 1) % 256); // Will roll over after 255 to avoid overflow
    }
}