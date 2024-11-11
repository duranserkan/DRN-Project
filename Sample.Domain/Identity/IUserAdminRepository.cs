using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;

namespace Sample.Domain.Identity;

public interface IUserAdminRepository
{
    Task<bool> AnySystemAdminExistsAsync();

    Task<IdentityResult> CreateSystemAdminForInitialSetup(SampleUser user, string inputPassword);
}