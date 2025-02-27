using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MfaExemptRequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class MfaExemptHandler : AuthorizationHandler<MfaExemptRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaExemptRequirement requirement)
    {
        var authenticated = context.User.Identities.Any(i => i.IsAuthenticated);
        if (authenticated)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}