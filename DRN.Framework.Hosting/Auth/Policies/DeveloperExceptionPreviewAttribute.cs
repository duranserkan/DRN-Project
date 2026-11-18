using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace DRN.Framework.Hosting.Auth.Policies;

/// <summary>Requires both the configured diagnostics policy and built-in viewer access.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DeveloperExceptionPreviewAttribute() : AuthorizeAttribute(DeveloperExceptionAccess.Policy), IAuthorizationRequirementData
{
    private static readonly AssertionRequirement ViewerAccess = new(context =>
        context.Resource is HttpContext http &&
        http.RequestServices.GetService<IAppSettings>() is { } settings &&
        DeveloperExceptionAccess.Allows(settings, context.User, http.RequestServices.GetService<AuthenticationClaimConfig>()));

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [ViewerAccess];
}
