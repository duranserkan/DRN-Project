using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Identity;

public interface IUserAdminRepository
{
    Task<bool> AnySystemAdminExistsAsync();

    Task<IdentityResult> CreateSystemAdminForInitialSetup(IdentityUser user, string inputPassword);
}