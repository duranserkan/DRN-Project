using System.Net;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Test.Utils.Hosting.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit.Sdk;

namespace DRN.Test.Integration.Tests.Framework.Hosting.Auth;

public class AuthenticationConfigurationTests
{
    [Theory]
    [IsolatedHostingData("implicit", 0, null)]
    [IsolatedHostingData("services-only", 0, null)]
    [IsolatedHostingData("single", 1, AuthenticationConfigurationTestProgram.FirstScheme)]
    [IsolatedHostingData("multiple", 2, null)]
    [IsolatedHostingData("default", 2, AuthenticationConfigurationTestProgram.FirstScheme)]
    [IsolatedHostingData("authenticate-only", 2, AuthenticationConfigurationTestProgram.FirstScheme)]
    public async Task Startup_And_Anonymous_Endpoints_Should_Not_Require_Identity_Or_A_Default_Scheme(
        DrnTestContext context, string mode, int schemeCount, string? authenticateScheme)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration(AuthenticationConfigurationTestProgram.ModeKey, mode);
        var application = context.ApplicationContext.CreateApplication<AuthenticationConfigurationTestProgram>();
        using var client = application.CreateClient();
        client.DefaultRequestHeaders.Add(AuthenticationConfigurationTestProgram.CredentialHeader, "mfa");
        using var response = await client.GetAsync(AuthenticationConfigurationTestProgram.Root + "/anonymous");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("anonymous");
        response.Headers.GetValues("X-After-Authentication").Should().Equal(authenticateScheme == null ? "anonymous" : "authenticated");
        var schemes = application.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemes.GetAllSchemesAsync()).Should().HaveCount(schemeCount);
        ((await schemes.GetDefaultAuthenticateSchemeAsync())?.Name).Should().Be(authenticateScheme);
        var expectedChallenge = mode is "single" or "default" ? AuthenticationConfigurationTestProgram.FirstScheme : null;
        ((await schemes.GetDefaultChallengeSchemeAsync())?.Name).Should().Be(expectedChallenge);
        ((await schemes.GetDefaultForbidSchemeAsync())?.Name).Should().Be(expectedChallenge);

        using var scope = application.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IAuthenticationService>().Should().NotBeNull();
        scope.ServiceProvider.GetService<ISecurityStampValidator>().Should().BeNull();
        scope.ServiceProvider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.OnRefreshingPrincipal
            .Should().NotBeNull();
        var authorization = application.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        authorization.DefaultPolicy.Requirements.Should().Contain(requirement => requirement is MfaRequirement);
        authorization.FallbackPolicy!.Requirements.Should().Contain(requirement => requirement is MfaRequirement);
        application.Services.GetRequiredService<ConfigurationExceptionRecorder>().Exceptions.Should().BeEmpty();
    }

    [Theory]
    [IsolatedHostingData("single", "protected", "", HttpStatusCode.Unauthorized)]
    [IsolatedHostingData("single", "protected", "plain", HttpStatusCode.Forbidden)]
    [IsolatedHostingData("single", "protected", "mfa", HttpStatusCode.OK)]
    [IsolatedHostingData("single", "fallback", "", HttpStatusCode.Unauthorized)]
    [IsolatedHostingData("single", "fallback", "plain", HttpStatusCode.Forbidden)]
    [IsolatedHostingData("single", "fallback", "mfa", HttpStatusCode.OK)]
    [IsolatedHostingData("default", "protected", "", HttpStatusCode.Unauthorized)]
    [IsolatedHostingData("default", "protected", "plain", HttpStatusCode.Forbidden)]
    [IsolatedHostingData("default", "protected", "mfa", HttpStatusCode.OK)]
    [IsolatedHostingData("multiple", "selected", "", HttpStatusCode.Unauthorized)]
    [IsolatedHostingData("multiple", "selected", "plain", HttpStatusCode.Forbidden)]
    [IsolatedHostingData("multiple", "selected", "mfa", HttpStatusCode.OK)]
    public async Task NonIdentity_Schemes_Should_Preserve_Mfa_And_Use_The_Effective_Scheme(
        DrnTestContext context, string mode, string endpoint, string credential, HttpStatusCode expected)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration(AuthenticationConfigurationTestProgram.ModeKey, mode);
        var application = context.ApplicationContext.CreateApplication<AuthenticationConfigurationTestProgram>();
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthenticationConfigurationTestProgram.Root + "/" + endpoint);
        if (credential.Length > 0) request.Headers.Add(AuthenticationConfigurationTestProgram.CredentialHeader, credential);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
        response.Headers.GetValues("X-Before-Authentication").Should().Equal("anonymous");
        response.Headers.GetValues("X-After-Authentication").Should().Equal(
            mode != "multiple" && credential.Length > 0 ? "authenticated" : "anonymous");
        var scheme = endpoint == "selected" ? AuthenticationConfigurationTestProgram.SecondScheme : AuthenticationConfigurationTestProgram.FirstScheme;
        if (expected == HttpStatusCode.OK)
            (await response.Content.ReadAsStringAsync()).Should().Be($"{scheme}|regression-user|True");
        else
        {
            response.Headers.GetValues(expected == HttpStatusCode.Unauthorized ? "X-Challenge-Scheme" : "X-Forbid-Scheme")
                .Should().Equal(scheme);
            response.Headers.Contains("X-Protected-Endpoint").Should().BeFalse();
        }
        application.Services.GetRequiredService<ConfigurationExceptionRecorder>().Exceptions.Should().BeEmpty();
    }

    [Theory]
    [IsolatedHostingData("implicit", "protected", "", "DefaultChallengeScheme")]
    [IsolatedHostingData("implicit", "fallback", "", "DefaultChallengeScheme")]
    [IsolatedHostingData("services-only", "protected", "", "DefaultChallengeScheme")]
    [IsolatedHostingData("multiple", "protected", "", "DefaultChallengeScheme")]
    [IsolatedHostingData("multiple", "protected", "mfa", "DefaultChallengeScheme")]
    [IsolatedHostingData("authenticate-only", "protected", "plain", "DefaultForbidScheme")]
    public async Task Missing_Scheme_Selection_Should_Fail_Closed_With_An_Actionable_Server_Diagnostic(
        DrnTestContext context, string mode, string endpoint, string credential, string missingDefault)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        context.AddToConfiguration(AuthenticationConfigurationTestProgram.ModeKey, mode);
        var application = context.ApplicationContext.CreateApplication<AuthenticationConfigurationTestProgram>();
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthenticationConfigurationTestProgram.Root + "/" + endpoint);
        if (credential.Length > 0) request.Headers.Add(AuthenticationConfigurationTestProgram.CredentialHeader, credential);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.Contains("X-Protected-Endpoint").Should().BeFalse();
        var exception = application.Services.GetRequiredService<ConfigurationExceptionRecorder>().Exceptions.Should().ContainSingle().Which;
        exception.Should().BeOfType<InvalidOperationException>();
        exception.Message.Should().Contain(missingDefault).And.Contain("AddAuthentication");
        (await response.Content.ReadAsStringAsync()).Should().NotBe(
            $"{AuthenticationConfigurationTestProgram.FirstScheme}|regression-user|True");
    }

    [Theory]
    [IsolatedHostingData("anonymous")]
    [IsolatedHostingData("self")]
    [IsolatedHostingData("inline")]
    public async Task Security_Header_Customization_Should_Reach_Default_And_Named_Policy_Responses(
        DrnTestContext context, string endpoint)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        var application = context.ApplicationContext.CreateApplication<AuthenticationConfigurationTestProgram>();
        using var client = application.CreateClient();

        using var response = await client.GetAsync(AuthenticationConfigurationTestProgram.Root + "/" + endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Program-Customization").Should().Equal("preserved");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();
        var scriptPolicy = response.Headers.GetValues("Content-Security-Policy").Single()
            .Split(';', StringSplitOptions.TrimEntries).Single(directive => directive.StartsWith("script-src ", StringComparison.Ordinal));
        if (endpoint == "inline") scriptPolicy.Should().Be("script-src 'self' 'unsafe-inline'");
        if (endpoint == "self") scriptPolicy.Should().Be("script-src 'self'");
        if (endpoint == "anonymous") scriptPolicy.Should().Contain("'nonce-");
    }

    // The integration assembly's startup job provisions Sample/Nexus databases. These hosts need neither.
    // Use the existing no-startup context constructor; do not change the shared startup job or production code.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    private sealed class IsolatedHostingDataAttribute(params object?[] values) : DataSelfAttribute
    {
        public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
        {
            var context = new DrnTestContext(testMethod, triggerStartUp: false);
            disposalTracker.Add(context);
            object?[] data = [context, .. values];
            context.MethodContext.SetTestData(data!);
            return ValueTask.FromResult<IReadOnlyCollection<ITheoryDataRow>>([new TheoryDataRow(data)]);
        }

        public override bool SupportsDiscoveryEnumeration() => false;
    }
}
