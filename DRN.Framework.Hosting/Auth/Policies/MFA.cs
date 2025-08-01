using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Framework.Hosting.Auth.Policies;

public class MfaRequirement : IAuthorizationRequirement;

[Singleton<IAuthorizationHandler>(tryAdd: false)]
public class RequireMfaHandler : AuthorizationHandler<MfaRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MfaRequirement requirement)
    {
        // Enforce MFA if not exemption configured such as bearer token auth
        if (MfaFor.MfaCompleted)
            context.Succeed(requirement);
        else if (((ScopedUser)ScopeContext.User).HasExemptionScheme)
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}

/// <summary>
/// Required to configure MFA Exemption. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFAExemption"/>,
/// </summary>
public class MfaExemptionConfig
{
    public IReadOnlyList<string> ExemptAuthSchemes { get; init; } = [];
}

[Singleton<MfaExemptionOptions>]
public class MfaExemptionOptions
{
    public IReadOnlyList<string> ExemptAuthSchemes { get; internal set; } = [];

    internal void MapFromConfig(MfaExemptionConfig config)
    {
        ExemptAuthSchemes = config.ExemptAuthSchemes;
    }
}