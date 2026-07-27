using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MfaExemptRequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class MfaExemptHandler : AuthorizationHandler<MfaExemptRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaExemptRequirement requirement)
    {
        if (AuthenticationFor.IsAuthenticated(context.User))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
