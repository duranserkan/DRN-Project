using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace Sample.Hosted.Auth.Policies;

public class MFARequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMFAHandler : AuthorizationHandler<MFARequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MFARequirement requirement)
    {
        // Check if the authentication scheme is Bearer
        var authScheme = context.User.Identity?.AuthenticationType;
        if (authScheme == "Bearer")
        {
            // Exempt bearer token holders from MFA requirement
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // For other authentication schemes, enforce MFA
        if (context.User.HasClaim(c => c.Type == "amr" && c.Value == "mfa"))
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}