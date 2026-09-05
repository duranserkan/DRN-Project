using System.Net;
using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class AuthenticationClaimConfigTests
{
    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void Scoped_Name_Should_Follow_The_Native_Primary_Identity(bool hasPrimaryName)
    {
        var primary = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "primary");
        if (hasPrimaryName) primary.AddClaim(new Claim(ClaimTypes.Name, "Primary"));
        var secondary = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user"), new Claim(ClaimTypes.Name, "Secondary")
        ], "secondary");
        var principal = new ClaimsPrincipal([primary, secondary]);
        var scoped = ScopedUser.FromClaimsPrincipal(principal);
        scoped.Name.Should().Be(hasPrimaryName ? "Primary" : null);
        scoped.Name.Should().Be(principal.Identity!.Name);
    }

    [Theory]
    [DataInlineUnit("external-id", true)]
    [DataInlineUnit("uid", true)]
    [DataInlineUnit("sub", false)]
    [DataInlineUnit("UID", false)]
    public void Custom_Subject_Should_Accept_Only_Explicit_Types_And_Reject_Conflicting_Defaults(string type, bool accepted)
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid", "external-id") };
        var identity = new ClaimsIdentity([new Claim(type, "user")], "external");
        (SubjectClaims.Find(identity, config)?.Value).Should().Be(accepted ? "user" : null);
        if (accepted)
        {
            identity.AddClaim(new Claim("sub", "other"));
            SubjectClaims.Find(identity, config).Should().BeNull();
        }
    }

    [Theory]
    [DataInlineUnit("matching", true)]
    [DataInlineUnit("missing", false)]
    [DataInlineUnit("conflicting-account", false)]
    [DataInlineUnit("conflicting-issuer", false)]
    public async Task Scoped_User_Ambient_Mfa_Handler_And_Rate_Limit_Should_Share_The_Mapping(
        DrnTestContextUnit context, string scenario, bool valid)
    {
        var mapping = new AuthenticationClaimConfig
        {
            Subject = new("uid"), Name = new("display"), Email = new("mail"), Roles = new("app-role")
        };
        context.ServiceCollection.AddSingleton(mapping);
        var identity = new ClaimsIdentity([
            new Claim("display", "User"), new Claim("mail", "user@example.test"), new Claim("app-role", "reader"),
            new Claim(AuthClaimTypes.Name, "ignored name"), new Claim(AuthClaimTypes.Email, "ignored@example.test"),
            new Claim(ClaimTypes.Role, "ignored-role"), new Claim(AuthClaimTypes.AuthenticationMethods, AuthMethodValues.MultiFactor)
        ], "provider");
        if (scenario != "missing")
            identity.AddClaim(new Claim("uid", "user", ClaimValueTypes.String, "issuer"));
        if (scenario.StartsWith("conflicting", StringComparison.Ordinal))
            identity.AddClaim(new Claim(AuthClaimTypes.Subject, scenario == "conflicting-account" ? "other" : "user",
                ClaimValueTypes.String, scenario == "conflicting-issuer" ? "other-issuer" : "issuer"));
        var principal = new ClaimsPrincipal(identity);
        var user = ActivatorUtilities.CreateInstance<ScopedUser>(context);
        user.SetUser(principal);
        user.Id.Should().Be(valid ? "user" : null);
        user.Name.Should().Be("User");
        user.Email.Should().Be("user@example.test");
        user.IsInRole("reader").Should().BeTrue();
        user.IsInRole("ignored-role").Should().BeFalse();
        principal.HasClaim(claim => claim.Type == AuthClaimTypes.Subject).Should().Be(scenario.StartsWith("conflicting", StringComparison.Ordinal));

        ScopeContext.InitializeForTest(context, scopedUser: user);
        MfaFor.MfaCompleted.Should().Be(valid);
        var requirement = new MfaRequirement();
        var authorization = new AuthorizationHandlerContext([requirement], principal, null);
        await ActivatorUtilities.CreateInstance<RequireMfaHandler>(context).HandleAsync(authorization);
        authorization.HasSucceeded.Should().Be(valid);

        var http = new DefaultHttpContext { RequestServices = context, User = principal };
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        RateLimitPartitionKeys.GetPostAuthPartitionKey(http).Should().Be(valid ? "user:provider:user" : "ip:127.0.0.1");

        identity.RemoveClaim(identity.FindFirst("display")!);
        identity.RemoveClaim(identity.FindFirst("mail")!);
        identity.RemoveClaim(identity.FindFirst("app-role")!);
        user.SetUser(principal);
        user.Name.Should().BeNull();
        user.Email.Should().BeNull();
        user.IsInRole("ignored-role").Should().BeFalse();
    }

    [Fact]
    public void Default_Aliases_Should_Reach_Scoped_Consumers_Without_Rewriting_Claims()
    {
        var source = new ClaimsIdentity([
            new Claim("sub", "user"), new Claim("name", "User"),
            new Claim("email", "user@example.test"), new Claim("roles", "reader")
        ], "external");
        var principal = new ClaimsPrincipal(source);
        var user = ScopedUser.FromClaimsPrincipal(principal);
        user.Id.Should().Be("user");
        user.Name.Should().Be("User");
        user.Email.Should().Be("user@example.test");
        user.IsInRole("reader").Should().BeTrue();
        principal.Identity!.Name.Should().BeNull();
        principal.IsInRole("reader").Should().BeFalse();
        source.Claims.Select(claim => claim.Type).Should().Equal("sub", "name", "email", "roles");
    }

    [Fact]
    public void Different_Applications_Should_Not_Share_Mappings_And_Defaults_Should_Still_Work()
    {
        var identity = new ClaimsIdentity([new Claim("uid", "user"), new Claim("account", "other")], "provider");
        var principal = new ClaimsPrincipal(identity);
        using var first = new ServiceCollection().AddSingleton(new AuthenticationClaimConfig { Subject = new("uid") }).BuildServiceProvider();
        using var second = new ServiceCollection().AddSingleton(new AuthenticationClaimConfig { Subject = new("account") }).BuildServiceProvider();
        var firstUser = ActivatorUtilities.CreateInstance<ScopedUser>(first);
        var secondUser = ActivatorUtilities.CreateInstance<ScopedUser>(second);
        firstUser.SetUser(principal);
        secondUser.SetUser(principal);
        firstUser.Id.Should().Be("user");
        secondUser.Id.Should().Be("other");
        ScopedUser.FromClaimsPrincipal(principal).Id.Should().BeNull();

        var legacy = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "legacy"), new Claim(ClaimTypes.Name, "Legacy User")
        ], "Identity.Application"));
        ScopedUser.FromClaimsPrincipal(legacy).Id.Should().Be("legacy");
        ScopedUser.FromClaimsPrincipal(legacy).Name.Should().Be("Legacy User");
    }

    [Theory]
    [DataInlineUnit("subject")]
    [DataInlineUnit("name")]
    [DataInlineUnit("email")]
    [DataInlineUnit("role")]
    public void Explicit_Empty_Mappings_Should_Be_Rejected(string field)
    {
        var create = () => field switch
        {
            "subject" => new AuthenticationClaimConfig { Subject = new(" ") },
            "name" => new AuthenticationClaimConfig { Name = new(" ") },
            "email" => new AuthenticationClaimConfig { Email = new(" ") },
            _ => new AuthenticationClaimConfig { Roles = new(" ") }
        };
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Defaults_And_Aliases_Should_Be_Explicit_Immutable_And_Replaced_Together()
    {
        var defaults = AuthenticationClaimConfig.Default;
        defaults.Subject.Type.Should().Be(ClaimTypes.NameIdentifier);
        defaults.Subject.Aliases.Should().Equal("sub");
        defaults.Name.Type.Should().Be(ClaimTypes.Name);
        defaults.Name.Aliases.Should().Equal("name");
        defaults.Email.Type.Should().Be(ClaimTypes.Email);
        defaults.Email.Aliases.Should().Equal("email");
        defaults.Roles.Type.Should().Be(ClaimTypes.Role);
        defaults.Roles.Aliases.Should().Equal("roles");
        defaults.Mfa.Should().Be(MfaClaimConfig.AspNetIdentity);
        string[] aliases = ["external-id"];
        var config = defaults with { Subject = new("uid", aliases) };
        aliases[0] = "sub";
        config.Subject.Aliases.Should().Equal("external-id");
        config.Subject.Accepts("sub").Should().BeFalse();
        var duplicate = () => new AuthenticationClaimConfig.ClaimMapping("uid", "UID");
        duplicate.Should().Throw<ArgumentException>();
        var emptyAlias = () => new AuthenticationClaimConfig.ClaimMapping("uid", " ");
        emptyAlias.Should().Throw<ArgumentException>();
        var nullMapping = () => new AuthenticationClaimConfig { Subject = null! };
        nullMapping.Should().Throw<ArgumentNullException>();
        var nullMfa = () => new AuthenticationClaimConfig { Mfa = null! };
        nullMfa.Should().Throw<ArgumentNullException>();
    }
}
