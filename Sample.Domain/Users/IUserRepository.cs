using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Users;

public interface IUserRepository
{
    Task<bool> AnySystemAdminExistsAsync();

    Task<IdentityResult> CreateSystemAdminForInitialSetup(IdentityUser user, string inputPassword);
}