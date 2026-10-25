using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Sample.Domain.Identity;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Unit.Tests.Sample;

public class DeveloperToolsTests
{
    [Theory]
    [DataInlineUnit(true, false, true, null, "true", false, false)]
    [DataInlineUnit(true, true, false, null, "true", false, false)]
    [DataInlineUnit(true, true, true, null, "true", true, true)]
    [DataInlineUnit(false, false, true, UserRoles.SystemAdmin, "true", false, false)]
    [DataInlineUnit(false, true, false, UserRoles.SystemAdmin, "true", false, false)]
    [DataInlineUnit(false, true, true, UserRoles.SystemAdmin, "true", true, true)]
    [DataInlineUnit(false, true, true, null, "true", false, false)]
    [DataInlineUnit(false, true, true, UserClaims.SwaggerAccess, "True", true, false)]
    [DataInlineUnit(false, true, true, UserClaims.TestPagesAccess, "true", false, true)]
    [DataInlineUnit(false, true, true, UserClaims.SwaggerAccess, "false", false, false)]
    [DataInlineUnit(false, true, true, UserClaims.TestPagesAccess, "invalid", false, false)]
    [DataInlineUnit(false, false, true, UserClaims.SwaggerAccess, "true", false, false)]
    [DataInlineUnit(false, true, false, UserClaims.TestPagesAccess, "true", false, false)]
    public async Task Developer_Tools_Should_Require_Mfa_And_Environment_Or_Matching_Permission(
        bool development, bool authenticated, bool mfa, string? grant, string claimValue,
        bool swaggerAllowed, bool testPagesAllowed, IAppSettings settings)
    {
        settings.Features.Returns(new DrnAppFeatures());
        settings.IsDevelopmentEnvironment.Returns(development);
        settings.GetAppSpecificName("DataProtection").Returns("DeveloperToolsTests");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => new TestProgram().ConfigureAuthorization(options));
        services.AddSampleHostedServices(settings);
        services.AddSingleton<IAuthorizationPolicyProvider, MfaEnforcingAuthorizationPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, RequireMfaHandler>();
        using var provider = services.BuildServiceProvider();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")],
            authenticated ? "Test" : null);
        if (grant == UserRoles.SystemAdmin)
            identity.AddClaim(new Claim(ClaimTypes.Role, UserRoles.SystemAdmin));
        else if (grant != null)
            identity.AddClaim(new Claim(grant, claimValue));
        if (mfa) identity.AddClaim(new Claim("amr", "mfa"));

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(identity);
        (await authorization.AuthorizeAsync(user, null, UserClaims.SwaggerAccess)).Succeeded.Should().Be(swaggerAllowed);
        (await authorization.AuthorizeAsync(user, null, UserClaims.TestPagesAccess)).Succeeded.Should().Be(testPagesAllowed);
    }

    [Fact]
    public void Developer_Preview_Should_Reach_Endpoint_Authorization()
    {
        ExceptionPageAccessor.IsExceptionPage(Get.Page.Test.DeveloperView).Should().BeFalse();
        ExceptionPageAccessor.IsExceptionPage("/unrelated/DeveloperViewSuffix").Should().BeFalse();
        ExceptionPageAccessor.IsExceptionPage("/Developer/RuntimeExceptionPage").Should().BeTrue();
        ExceptionPageAccessor.IsExceptionPage("/Developer/CompilationExceptionPage").Should().BeTrue();
    }

    private sealed class TestProgram : SampleProgram
    {
        public void ConfigureAuthorization(AuthorizationOptions options) => ConfigureAuthorizationOptions(options);
    }
}
