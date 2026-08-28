using System.Security.Claims;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;
using Sample.Domain.Users;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IUserProfileRepository>]
public class UserProfileRepository(UserManager<SampleUser> userManager, IUserClaimRepository userClaimRepository, SampleIdentityContext context)
    : IUserProfileRepository
{
    public async Task<UserProfileEditResult> UpdateUserProfileAsync(UserProfileModel model, SampleUser user, ClaimsPrincipal principal)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            user.PhoneNumber = model.PhoneNumber;

            var identityResult = await userManager.UpdateAsync(user);
            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return new UserProfileEditResult(identityResult, user);
            }

            var claimResult = await userClaimRepository.UpdateSlimUiClaimAsync(user, model.SlimUI);
            if (!claimResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return new UserProfileEditResult(claimResult, user);
            }

            await transaction.CommitAsync();

            return new UserProfileEditResult(identityResult, user);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
