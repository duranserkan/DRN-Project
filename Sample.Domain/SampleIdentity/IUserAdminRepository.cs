using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.SampleIdentity;

public interface IUserAdminRepository
{
    Task<bool> AnySystemAdminExistsAsync();

    Task<IdentityResult> CreateSystemAdminForInitialSetup(SampleUser user, string inputPassword);
}
