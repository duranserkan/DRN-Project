using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Identity;

public interface IUserProfileRepository
{
    Task<UserProfileEditResult> UpdateUserProfileAsync(UserProfileModel model, IdentityUser user, ClaimsPrincipal principal);
}

public class UserProfileModel
{
    public string PhoneNumber { get; set; }

    public bool SlimUI { get; set; }
}

public class UserProfileEditResult(IdentityResult identityResult, IdentityUser identityUser)
{
    public IdentityResult IdentityResult { get; } = identityResult;
    public IdentityUser IdentityUser { get; } = identityUser;
}