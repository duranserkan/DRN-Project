using DRN.Framework.Utils.DependencyInjection.Attributes;

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
        if (authenticated)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}