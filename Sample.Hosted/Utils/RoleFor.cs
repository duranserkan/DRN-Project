using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Utils;

public class RoleFor
{
    public SystemRoleFor System { get; } = new();
}

public class SystemRoleFor
{
    public bool Admin => ScopeContext.IsUserInRole(UserRoles.SystemAdmin);
}