using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.SampleIdentity.Users;

public interface IUserAdminRepository
{
    Task<bool> AnySystemAdminExistsAsync();

    Task<IdentityResult> CreateSystemAdminForInitialSetup(SampleUser user, string inputPassword);
}
