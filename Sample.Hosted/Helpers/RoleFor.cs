using System.Diagnostics.CodeAnalysis;
using Sample.Domain.Identity;

namespace Sample.Hosted.Helpers;

public class RoleFor
{
    public SystemRoleFor System { get; } = new();
}

public class SystemRoleFor
{
    [SuppressMessage("SonarQube", "S2325", Justification = "Get.Role.System exposes the current user's role membership through this instance.")]
    public bool Admin => ScopeContext.IsUserInRole(UserRoles.SystemAdmin);
}
