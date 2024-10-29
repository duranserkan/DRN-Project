using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MFARequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMFAHandler : AuthorizationHandler<MFARequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MFARequirement requirement)
    {
        // Enforce MFA if not exemption configured such as bearer token auth
        if (MFAFor.MFACompleted)
            context.Succeed(requirement);
        else if (((ScopedUser)ScopeContext.User).HasExemptionSchemes)
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}

/// <summary>
/// Required to configure MFA Exemption. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFAExemption"/>,
/// </summary>
public class MFAExemptionConfig
{
    public IReadOnlyList<string> ExemptAuthSchemes { get; init; } = [];
}

[Singleton<MFAExemptionOptions>]
public class MFAExemptionOptions
{
    public IReadOnlyList<string> ExemptAuthSchemes { get; internal set; } = [];

    internal void MapFromConfig(MFAExemptionConfig config)
    {
        ExemptAuthSchemes = config.ExemptAuthSchemes;
    }
}