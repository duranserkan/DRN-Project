using System.Security.Claims;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IUserClaimRepository>]
public class UserClaimRepository(UserManager<IdentityUser> userManager) : IUserClaimRepository
{
    public async Task UpdateProfilePictureVersionClaimAsync(IdentityUser user, byte version)
    {
        var claims = await userManager.GetClaimsAsync(user);

        await UpdateClaim(user, UserClaims.PPVersion, version.ToString(), claims);
    }

    public async Task UpdateSlimUiClaimAsync(IdentityUser user, bool slimUi)
    {
        var claims = await userManager.GetClaimsAsync(user);

        await UpdateClaim(user, UserClaims.SlimUI, slimUi.ToString(), claims);
    }

    private async Task UpdateClaim(IdentityUser user, string claimType, string claimValue, IList<Claim> claims)
    {
        var existingVersionClaim = claims.FirstOrDefault(c => c.Type == claimType);
        if (existingVersionClaim != null)
            await userManager.ReplaceClaimAsync(user, existingVersionClaim, new Claim(claimType, claimValue));
        else
            await userManager.AddClaimAsync(user, new Claim(claimType, claimValue));
    }
}