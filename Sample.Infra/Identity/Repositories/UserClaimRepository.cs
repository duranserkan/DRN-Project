using System.Security.Claims;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;
using Sample.Domain.Users;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IUserClaimRepository>]
public class UserClaimRepository(UserManager<SampleUser> userManager) : IUserClaimRepository
{
    public async Task<IdentityResult> UpdateProfilePictureVersionClaimAsync(SampleUser user, byte version)
    {
        var claims = await userManager.GetClaimsAsync(user);

        return await UpdateClaimAsync(user, UserClaims.PPVersion, version.ToString(), claims);
    }

    public async Task<IdentityResult> UpdateSlimUiClaimAsync(SampleUser user, bool slimUi)
    {
        var claims = await userManager.GetClaimsAsync(user);

        return await UpdateClaimAsync(user, UserClaims.SlimUI, slimUi.ToString(), claims);
    }

    private async Task<IdentityResult> UpdateClaimAsync(SampleUser user, string claimType, string claimValue, IList<Claim> claims)
    {
        var existingVersionClaim = claims.FirstOrDefault(c => c.Type == claimType);
        if (existingVersionClaim != null)
            return await userManager.ReplaceClaimAsync(user, existingVersionClaim, new Claim(claimType, claimValue));

        return await userManager.AddClaimAsync(user, new Claim(claimType, claimValue));
    }
}
