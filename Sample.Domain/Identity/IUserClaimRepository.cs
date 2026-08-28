using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;

namespace Sample.Domain.Identity;

public interface IUserClaimRepository
{
    Task<IdentityResult> UpdateProfilePictureVersionClaimAsync(SampleUser user, byte version);
    Task<IdentityResult> UpdateSlimUiClaimAsync(SampleUser user, bool slimUi);
}
