using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Sample.Domain.Identity;

namespace Sample.Infra.Identity.Repositories;

[Scoped<IUserAdminRepository>]
public class UserAdminRepository(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    SampleIdentityContext identityContext,
    IMemoryCache cache) : IUserAdminRepository
{
    private const string SystemAdminUserCacheKey = "SystemAdminUserExists";
    private const string SystemAdminRole = "SystemAdmin";

    public async Task<bool> AnySystemAdminExistsAsync()
    {
        if (cache.TryGetValue(SystemAdminUserCacheKey, out bool adminUserExists))
            return adminUserExists;

        var usersInRole = await userManager.GetUsersInRoleAsync(SystemAdminRole);
        adminUserExists = usersInRole.Count > 0;

        if (adminUserExists)
            cache.Set(SystemAdminUserCacheKey, true);

        return adminUserExists;
    }

    public async Task<IdentityResult> CreateSystemAdminForInitialSetup(IdentityUser user, string inputPassword)
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
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<IdentityResult> AddSystemAdminAsync(IdentityUser user, string inputPassword)
    {
        var adminRoleExists = await roleManager.RoleExistsAsync(SystemAdminRole);
        if (!adminRoleExists)
        {
            var role = new IdentityRole(SystemAdminRole);
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
        var adminCreatedResult = await userManager.AddToRoleAsync(adminUser, SystemAdminRole);

        return adminCreatedResult;
    }
}