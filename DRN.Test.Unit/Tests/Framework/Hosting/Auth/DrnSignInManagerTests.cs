using System.Security.Claims;
using DRN.Framework.Hosting.Identity;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class DrnSignInManagerTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Identity_Factory_Should_Issue_Configured_Claims_And_Native_Metadata(bool custom)
    {
        var config = custom
            ? new AuthenticationClaimConfig { Subject = new("uid"), Name = new("display"), Email = new("mail"), Roles = new("app-role") }
            : AuthenticationClaimConfig.Default;
        var store = Substitute.For<IUserRoleStore<IdentityUser>, IUserEmailStore<IdentityUser>>();
        store.GetUserIdAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>()).Returns("user");
        store.GetUserNameAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>()).Returns("User");
        store.GetRolesAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IList<string>>(["Admin"]));
        ((IUserEmailStore<IdentityUser>)store).GetEmailAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>())
            .Returns("user@example.test");
        var services = new ServiceCollection().AddLogging().AddSingleton(config);
        services.AddAuthentication();
        services.AddSingleton<IUserStore<IdentityUser>>(store);
        services.AddSingleton(Substitute.For<IRoleStore<IdentityRole>>());
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = config.Subject.Type;
            options.ClaimsIdentity.UserNameClaimType = config.Name.Type;
            options.ClaimsIdentity.EmailClaimType = config.Email.Type;
            options.ClaimsIdentity.RoleClaimType = config.Roles.Type;
        }).AddRoles<IdentityRole>().AddSignInManager<DrnSignInManager<IdentityUser>>();
        using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<SignInManager<IdentityUser>>();

        var principal = await manager.CreateUserPrincipalAsync(new IdentityUser());

        var identity = principal.Identity.Should().BeOfType<ClaimsIdentity>().Which;
        identity.NameClaimType.Should().Be(config.Name.Type);
        identity.RoleClaimType.Should().Be(config.Roles.Type);
        identity.Name.Should().Be("User");
        principal.IsInRole("Admin").Should().BeTrue();
        principal.HasClaim(config.Subject.Type, "user").Should().BeTrue();
        principal.HasClaim(config.Email.Type, "user@example.test").Should().BeTrue();
        var scoped = new ScopedUser(config);
        scoped.SetUser(principal);
        scoped.Id.Should().Be("user");
        scoped.Name.Should().Be(identity.Name);
        scoped.Email.Should().Be("user@example.test");
        scoped.IsInRole("Admin").Should().BeTrue();
        principal.Claims.Select(claim => claim.Type).Should().BeEquivalentTo(
            config.Subject.Type, config.Name.Type, config.Email.Type, config.Roles.Type);
    }

    [Theory]
    [DataInlineUnit(false, "missing")]
    [DataInlineUnit(true, "missing")]
    [DataInlineUnit(true, "empty")]
    [DataInlineUnit(true, "conflicting-subject")]
    [DataInlineUnit(true, "conflicting-issuer")]
    [DataInlineUnit(true, "case-collision")]
    [DataInlineUnit(true, "anonymous")]
    public async Task Factory_Should_Require_Unambiguous_Authenticated_Account(bool custom, string scenario)
    {
        var config = custom ? new AuthenticationClaimConfig { Subject = new("uid") } : AuthenticationClaimConfig.Default;
        using var fixture = new SignInFixture(config);
        if (scenario is "missing" or "empty")
        {
            fixture.FactoryIdentity.RemoveClaim(fixture.FactoryIdentity.FindFirst(config.Subject.Type)!);
            if (scenario == "empty") fixture.FactoryIdentity.AddClaim(new Claim(config.Subject.Type, ""));
        }
        if (scenario == "conflicting-subject") fixture.FactoryIdentity.AddClaim(new Claim(config.Subject.Type, "other"));
        if (scenario == "conflicting-issuer")
            fixture.FactoryIdentity.AddClaim(new Claim(config.Subject.Type, "user", ClaimValueTypes.String, "other-issuer"));
        if (scenario == "case-collision") fixture.FactoryIdentity.AddClaim(new Claim("UID", "other"));
        if (scenario == "anonymous") fixture.FactoryIdentity = new ClaimsIdentity(fixture.FactoryIdentity.Claims);

        var create = () => fixture.Manager.CreateUserPrincipalAsync(fixture.User);

        await create.Should().ThrowAsync<System.Security.SecurityException>();
    }

    [Fact]
    public async Task Factory_Claims_And_Metadata_Should_Be_Preserved_Without_Alias_Projection()
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid"), Name = new("display", "username"), Roles = new("app-role", "group") };
        using var fixture = new SignInFixture(config);
        var source = fixture.FactoryIdentity;
        source.Label = "factory";
        source.BootstrapContext = "original-context";
        source.AddClaim(new Claim("username", "User", ClaimValueTypes.String, "issuer", "original"));
        source.FindFirst("username")!.Properties.Add("source", "factory");
        source.AddClaim(new Claim("group", "reader"));

        var principal = await fixture.Manager.CreateUserPrincipalAsync(fixture.User);

        var identity = principal.Identity.Should().BeOfType<ClaimsIdentity>().Which;
        identity.Should().NotBeSameAs(source);
        identity.NameClaimType.Should().Be(source.NameClaimType);
        identity.RoleClaimType.Should().Be(source.RoleClaimType);
        identity.Label.Should().Be(source.Label);
        identity.BootstrapContext.Should().BeSameAs(source.BootstrapContext);
        identity.Claims.Select(claim => claim.Type).Should().Equal(source.Claims.Select(claim => claim.Type));
        var alias = identity.FindFirst("username")!;
        alias.ValueType.Should().Be(ClaimValueTypes.String);
        alias.Issuer.Should().Be("issuer");
        alias.OriginalIssuer.Should().Be("original");
        alias.Properties["source"].Should().Be("factory");
        identity.HasClaim(config.Roles.Type, "reader").Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit("amr", "mfa", true)]
    [DataInlineUnit("completed", "yes", true)]
    [DataInlineUnit("completed", "yes", false)]
    public async Task SignIn_Should_Map_Additional_TwoFactor_Evidence_And_Ignore_Stored_Markers(
        string type, string value, bool verified)
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid"), Mfa = new(type, value) };
        using var fixture = new SignInFixture(config);
        fixture.FactoryIdentity.AddClaim(new Claim(type, value));
        fixture.FactoryIdentity.AddClaim(new Claim("amr", "mfa"));

        await fixture.Manager.SignInWithClaimsAsync(fixture.User, false,
            verified ? [new Claim("amr", "mfa")] : [new Claim("amr", "pwd")]);

        MfaPrincipal.IsCompleted(fixture.Http.User, config).Should().Be(verified);
        fixture.Http.User.HasClaim(type, value).Should().Be(verified);
        fixture.FactoryIdentity.HasClaim(type, value).Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("matching", true)]
    [DataInlineUnit("different-marker", true)]
    [DataInlineUnit("other-account", false)]
    [DataInlineUnit("conflicting-subject", false)]
    [DataInlineUnit("other-issuer", false)]
    public async Task Explicit_Refresh_Should_Preserve_Original_Evidence_Only_For_The_Same_Account(string scenario, bool allowed)
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid"), Mfa = new("completed", "yes") };
        using var fixture = new SignInFixture(config);
        var source = new ClaimsIdentity([
            new Claim("uid", scenario == "other-account" ? "other" : "user", ClaimValueTypes.String,
                scenario == "other-issuer" ? "other-issuer" : ClaimsIdentity.DefaultIssuer),
            new Claim("amr", "pwd"), new Claim("amr", "otp"), new Claim("completed", "yes"),
            new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64)
        ], IdentityConstants.ApplicationScheme);
        source.FindFirst("completed")!.Properties.Add("evidence", "original");
        if (scenario == "different-marker")
        {
            source.RemoveClaim(source.FindFirst("completed")!);
            source.AddClaim(new Claim("amr", "mfa"));
        }
        if (scenario == "conflicting-subject") source.AddClaim(new Claim(config.Subject.Type, "other"));
        var properties = new AuthenticationProperties { IsPersistent = true };
        fixture.Authentication.AuthenticateAsync(fixture.Http, IdentityConstants.ApplicationScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(source), properties, IdentityConstants.ApplicationScheme)));
        fixture.FactoryIdentity.AddClaim(new Claim("auth_time", "1800000000"));
        fixture.FactoryIdentity.AddClaim(new Claim("completed", "yes"));

        var refresh = () => fixture.Manager.RefreshSignInAsync(fixture.User);
        if (scenario == "other-issuer")
            await refresh.Should().ThrowAsync<System.Security.SecurityException>();
        else
            await refresh();

        if (!allowed)
        {
            await fixture.Authentication.DidNotReceive().SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string>(),
                Arg.Any<ClaimsPrincipal>(), Arg.Any<AuthenticationProperties>());
            return;
        }
        await fixture.Authentication.Received(1).SignInAsync(fixture.Http, IdentityConstants.ApplicationScheme,
            Arg.Any<ClaimsPrincipal>(), properties);
        var renewed = fixture.Http.User;
        string[] expectedMethods = scenario == "different-marker" ? ["pwd", "otp", "mfa"] : ["pwd", "otp"];
        renewed.FindAll("amr").Select(claim => claim.Value).Should().Equal(expectedMethods);
        if (scenario == "different-marker")
            renewed.FindFirst("completed").Should().BeNull();
        else
            renewed.FindFirst("completed")!.Properties["evidence"].Should().Be("original");
        renewed.FindFirst("auth_time")!.Value.Should().Be("1700000000");
        MfaPrincipal.IsCompleted(renewed, config).Should().Be(scenario != "different-marker");
        source.FindFirst("auth_time")!.Value.Should().Be("1700000000");
    }

    private sealed class SignInFixture : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly UserManager<IdentityUser> _users;
        public IdentityUser User { get; } = new() { Id = "user", UserName = "User" };
        public ClaimsIdentity FactoryIdentity { get; set; }
        public IAuthenticationService Authentication { get; } = Substitute.For<IAuthenticationService>();
        public DefaultHttpContext Http { get; }
        public DrnSignInManager<IdentityUser> Manager { get; }

        public SignInFixture(AuthenticationClaimConfig config)
        {
            var services = new ServiceCollection().AddLogging().AddSingleton(Authentication);
            var store = Substitute.For<IUserStore<IdentityUser>>();
            store.GetUserIdAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>()).Returns(call => ((IdentityUser)call[0]).Id);
            services.AddSingleton(store).AddIdentityCore<IdentityUser>(options => options.ClaimsIdentity.UserIdClaimType = config.Subject.Type);
            _services = services.BuildServiceProvider();
            _users = _services.GetRequiredService<UserManager<IdentityUser>>();
            Http = new DefaultHttpContext { RequestServices = _services };
            FactoryIdentity = new ClaimsIdentity([new Claim(config.Subject.Type, "user"), new Claim(ClaimTypes.Name, "User")],
                IdentityConstants.ApplicationScheme);
            var factory = Substitute.For<IUserClaimsPrincipalFactory<IdentityUser>>();
            factory.CreateAsync(Arg.Any<IdentityUser>()).Returns(_ => new ClaimsPrincipal(FactoryIdentity.Clone()));
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(Http);
            Manager = new DrnSignInManager<IdentityUser>(_users, accessor, factory,
                _services.GetRequiredService<IOptions<IdentityOptions>>(), _services.GetRequiredService<ILogger<SignInManager<IdentityUser>>>(),
                Substitute.For<IAuthenticationSchemeProvider>(), Substitute.For<IUserConfirmation<IdentityUser>>(), config);
        }

        public void Dispose() => _services.Dispose();
    }
}
