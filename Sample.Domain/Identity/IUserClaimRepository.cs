using Sample.Domain.Users;

namespace Sample.Domain.Identity;

public interface IUserClaimRepository
{
    Task UpdateProfilePictureVersionClaimAsync(SampleUser user, byte version);
    Task UpdateSlimUiClaimAsync(SampleUser user, bool slimUi);
}