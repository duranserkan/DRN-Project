using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Utils;
//Todo: unify all for values under single parent For object
public abstract class RoleFor
{
    public static SystemRoleFor System { get; } = new();
}

public class SystemRoleFor
{
    public bool Admin => ScopeContext.IsUserInRole(UserRoles.SystemAdmin);
}