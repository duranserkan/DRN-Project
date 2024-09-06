using System.Security.Claims;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IUserProfileRepository>]
public class UserProfileRepository(UserManager<IdentityUser> userManager, IUserClaimRepository userClaimRepository, SampleIdentityContext context)
    : IUserProfileRepository
{
    public async Task<UserProfileEditResult> UpdateUserProfileAsync(UserProfileModel model, IdentityUser user, ClaimsPrincipal principal)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            user.PhoneNumber = model.PhoneNumber;

            var identityResult = await userManager.UpdateAsync(user);
            var result = new UserProfileEditResult(identityResult, user);

            await userClaimRepository.UpdateSlimUiClaimAsync(user, model.SlimUI);
            await transaction.CommitAsync();

            return result;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}