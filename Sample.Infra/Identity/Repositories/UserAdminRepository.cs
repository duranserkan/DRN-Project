using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Sample.Domain.Identity;
using Sample.Domain.Users;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IUserAdminRepository>]
public class UserAdminRepository(
    UserManager<SampleUser> userManager,
    RoleManager<IdentityRole> roleManager,
    SampleIdentityContext identityContext,
    IMemoryCache cache) : IUserAdminRepository
{
    private const string SystemAdminUserCacheKey = "SystemAdminUserExists";

    public async Task<bool> AnySystemAdminExistsAsync()
    {
        if (cache.TryGetValue(SystemAdminUserCacheKey, out bool adminUserExists))
            return adminUserExists;

        var usersInRole = await userManager.GetUsersInRoleAsync(UserRoles.SystemAdmin);
        adminUserExists = usersInRole.Count > 0;

        if (adminUserExists)
            cache.Set(SystemAdminUserCacheKey, true);

        return adminUserExists;
    }

    public async Task<IdentityResult> CreateSystemAdminForInitialSetup(SampleUser user, string inputPassword)
    {
        await using var transaction = await identityContext.Database.BeginTransactionAsync();
        try
        {
            var adminCreatedResult = await AddSystemAdminAsync(user, inputPassword);
            if (adminCreatedResult.Succeeded)
            {
                await transaction.CommitAsync();
                cache.Set(SystemAdminUserCacheKey, true);
            }
            else
                await transaction.RollbackAsync();

            return adminCreatedResult;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<IdentityResult> AddSystemAdminAsync(SampleUser user, string inputPassword)
    {
        var adminRoleExists = await roleManager.RoleExistsAsync(UserRoles.SystemAdmin);
        if (!adminRoleExists)
        {
            var role = new IdentityRole(UserRoles.SystemAdmin);
            var roleCreatedResult = await roleManager.CreateAsync(role);

            if (!roleCreatedResult.Succeeded)
                return roleCreatedResult;
        }

        var existingUser = await userManager.Users.SingleOrDefaultAsync(u => u.Email == user.Email);
        if (existingUser == null)
        {
            var userCreateResult = await userManager.CreateAsync(user, inputPassword);
            if (!userCreateResult.Succeeded)
                return userCreateResult;
        }

        var adminUser = existingUser ?? user;
        var adminCreatedResult = await userManager.AddToRoleAsync(adminUser, UserRoles.SystemAdmin);

        return adminCreatedResult;
    }
}