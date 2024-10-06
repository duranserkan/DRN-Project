using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Hosted.Auth.Claims;

namespace Sample.Hosted.Auth.Policies;

public class MFAExemptRequirement : IAuthorizationRequirement
{
}

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class MFAExemptHandler : AuthorizationHandler<MFAExemptRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MFAExemptRequirement requirement)
    {
        var authenticated = context.User.Identities.Any(i => i.IsAuthenticated);
        if (authenticated || ClaimContext.MFAInProgress)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}