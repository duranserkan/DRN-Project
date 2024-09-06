using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Identity;

public interface IUserClaimRepository
{
    Task UpdateProfilePictureVersionClaimAsync(IdentityUser user, byte version);
    Task UpdateSlimUiClaimAsync(IdentityUser user, bool slimUi);
}