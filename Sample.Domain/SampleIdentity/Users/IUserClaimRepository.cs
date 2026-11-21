using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.SampleIdentity.Users;

public interface IUserClaimRepository
{
    Task<IdentityResult> UpdateProfilePictureVersionClaimAsync(SampleUser user, byte version);
    Task<IdentityResult> UpdateSlimUiClaimAsync(SampleUser user, bool slimUi);
}
