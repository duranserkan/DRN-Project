using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MFARequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMFAHandler : AuthorizationHandler<MFARequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MFARequirement requirement)
    {
        var authScheme = context.User.Identity?.AuthenticationType;
        if (authScheme == "Bearer")
        {
            // Exempt bearer token holders from MFA requirement
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // For other authentication schemes, enforce MFA
        if (MFAFor.MFACompleted)
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}