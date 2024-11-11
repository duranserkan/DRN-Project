using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;

namespace Sample.Domain.Identity;

public interface IUserProfileRepository
{
    Task<UserProfileEditResult> UpdateUserProfileAsync(UserProfileModel model, SampleUser user, ClaimsPrincipal principal);
}

public class UserProfileModel
{
    public string PhoneNumber { get; init; } = string.Empty;

    public bool SlimUI { get; init; }
}

public class UserProfileEditResult(IdentityResult identityResult, SampleUser identityUser)
{
    public IdentityResult IdentityResult { get; } = identityResult;
    public SampleUser IdentityUser { get; } = identityUser;
}