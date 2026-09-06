using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramBaseSecurityOptionsTests
{
    [Fact]
    public async Task Builder_Should_Map_All_Identity_Claims_Without_Replacing_Unrelated_Options()
    {
        using var appSettings = (AppSettings)AppSettings.Development();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        using var configuration = builder.Configuration;
        new TestProgram().RegisterDefaults(builder, appSettings);
        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = "old-subject";
            options.ClaimsIdentity.UserNameClaimType = "old-name";
            options.ClaimsIdentity.EmailClaimType = "old-email";
            options.ClaimsIdentity.RoleClaimType = "old-role";
            options.ClaimsIdentity.SecurityStampClaimType = "custom-stamp";
            options.Password.RequiredLength = 19;
            options.Lockout.MaxFailedAccessAttempts = 7;
        });
        await using var provider = builder.Services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var claims = provider.GetRequiredService<AuthenticationClaimConfig>();

        claims.Should().BeSameAs(TestProgram.Claims);
        options.ClaimsIdentity.UserIdClaimType.Should().Be("uid");
        options.ClaimsIdentity.UserNameClaimType.Should().Be("display");
        options.ClaimsIdentity.EmailClaimType.Should().Be("mail");
        options.ClaimsIdentity.RoleClaimType.Should().Be("app-role");
        options.ClaimsIdentity.SecurityStampClaimType.Should().Be("custom-stamp");
        options.Password.RequiredLength.Should().Be(19);
        options.Lockout.MaxFailedAccessAttempts.Should().Be(7);
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task Builder_Should_Register_Cookie_Consent_And_Antiforgery_Defaults(bool isDevelopment)
    {
        var appSettings = CreateAppSettings(isDevelopment);
        appSettings.Features.Returns(new DrnAppFeatures());
        appSettings.Localization.Returns(new DrnLocalizationSettings());
        appSettings.GetAppSpecificName("CookieConsent").Returns("_test_Consent");
        appSettings.GetAppSpecificName("Antiforgery").Returns("_test_Antiforgery");
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        using var configuration = builder.Configuration;
        new TestProgram().RegisterDefaults(builder, appSettings);
        await using var provider = builder.Services.BuildServiceProvider();

        var cookies = provider.GetRequiredService<IOptions<CookiePolicyOptions>>().Value;
        var tempData = provider.GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value;
        var antiforgery = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        cookies.Secure.Should().Be(isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
        cookies.MinimumSameSitePolicy.Should().Be(SameSiteMode.Strict);
        cookies.HttpOnly.Should().Be(HttpOnlyPolicy.None);
        cookies.ConsentCookie.Name.Should().Be("_test_Consent");
        cookies.CheckConsentNeeded.Should().NotBeNull();
        cookies.CheckConsentNeeded!(new DefaultHttpContext()).Should().BeTrue();
        cookies.ConsentCookieValue.Should().NotBeNullOrEmpty();
        tempData.Cookie.HttpOnly.Should().BeTrue();
        tempData.Cookie.IsEssential.Should().BeTrue();
        antiforgery.Cookie.Name.Should().Be("_test_Antiforgery");
        antiforgery.Cookie.HttpOnly.Should().BeTrue();
        antiforgery.Cookie.IsEssential.Should().BeTrue();
        antiforgery.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.SameAsRequest);
    }

    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public void SecurityHeaders_Should_Include_Hsts_Only_Outside_Development(bool isDevelopment)
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var policies = new HeaderPolicyCollection();

        new TestProgram().ConfigureHeaders(policies, provider, CreateAppSettings(isDevelopment));

        policies.ContainsKey("Strict-Transport-Security").Should().Be(!isDevelopment);
        policies.ContainsKey("Content-Security-Policy").Should().BeTrue();
        policies.ContainsKey("X-Frame-Options").Should().BeTrue();
        policies.ContainsKey("X-Content-Type-Options").Should().BeTrue();
        policies.ContainsKey("Referrer-Policy").Should().BeTrue();
    }

    [Fact]
    public void HostFiltering_Should_Preserve_A_Preconfigured_Production_Allowlist()
    {
        var appSettings = CreateAppSettings(false, ("AllowedHosts", "configured.example"));
        var hosts = new List<string> { "preconfigured.example" };
        var options = new HostFilteringOptions { AllowedHosts = hosts };

        new TestProgram().ExposeConfigureHostFilteringOptions(appSettings)(options);

        options.AllowedHosts.Should().BeSameAs(hosts);
        options.AllowedHosts.Should().Equal("preconfigured.example");
    }

    [Theory]
    [DataInlineUnit("*")]
    [DataInlineUnit(" * ")]
    public void HostFiltering_Should_Reject_A_Preconfigured_Wildcard_Despite_Valid_Configuration(string wildcard)
    {
        var appSettings = CreateAppSettings(false, ("AllowedHosts", "configured.example"));
        var options = new HostFilteringOptions { AllowedHosts = ["preconfigured.example", wildcard] };

        var act = () => new TestProgram().ExposeConfigureHostFilteringOptions(appSettings)(options);

        act.Should().Throw<ConfigurationException>().WithMessage("AllowedHosts cannot contain '*' outside Development.");
    }

    [Theory]
    [DataInlineUnit(null, "AllowedHosts must be configured outside Development.")]
    [DataInlineUnit("*", "AllowedHosts cannot contain '*' outside Development.")]
    public void ConfigureHostFilteringOptions_Should_Reject_Missing_Or_Wildcard_Production_Hosts(
        string? allowedHosts, string expectedMessage)
    {
        var appSettings = allowedHosts == null
            ? CreateAppSettings(isDevelopment: false)
            : CreateAppSettings(isDevelopment: false, ("AllowedHosts", allowedHosts));
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage(expectedMessage);
    }
    
    [Fact]
    public void ConfigureHostFilteringOptions_Should_Allow_Development_Fallback()
    {
        var appSettings = CreateAppSettings(isDevelopment: true);
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        configure(options);

        options.AllowedHosts.Should().Equal("*");
    }
    
    [Fact]
    public void ConfigureHostFilteringOptions_Should_Use_Configured_Production_Hosts()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("AllowedHosts", "example.com;api.example.com"));
        var options = new HostFilteringOptions();
        var configure = new TestProgram().ExposeConfigureHostFilteringOptions(appSettings);

        configure(options);

        options.AllowedHosts.Should().Equal("example.com", "api.example.com");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Use_Default_Trusted_Networks_And_ForwardLimit_Of_Two()
    {
        var appSettings = CreateAppSettings(isDevelopment: true);
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
        options.ForwardLimit.Should().Be(2);
        options.KnownIPNetworks.Should().HaveCount(5);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "127.0.0.0" && n.PrefixLength == 8);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "::1" && n.PrefixLength == 128);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "10.0.0.0" && n.PrefixLength == 8);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "172.16.0.0" && n.PrefixLength == 12);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "192.168.0.0" && n.PrefixLength == 16);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Bind_Configuration_Section_When_Present()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("ForwardedHeaders:ForwardLimit", "3"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
        options.ForwardLimit.Should().Be(3);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "10.0.0.0" && n.PrefixLength == 8);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Exclude_Private_Networks_When_TrustPrivateNetworks_Is_False()
    {
        var appSettings = CreateAppSettings(isDevelopment: false, ("ForwardedHeaders:TrustPrivateNetworks", "false"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.All);
        options.ForwardLimit.Should().Be(2);
        options.KnownIPNetworks.Should().HaveCount(2);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "127.0.0.0" && n.PrefixLength == 8);
        options.KnownIPNetworks.Should().Contain(n => n.BaseAddress.ToString() == "::1" && n.PrefixLength == 128);
        options.KnownIPNetworks.Should().NotContain(n => n.BaseAddress.ToString() == "10.0.0.0");
        options.KnownIPNetworks.Should().NotContain(n => n.BaseAddress.ToString() == "172.16.0.0");
        options.KnownIPNetworks.Should().NotContain(n => n.BaseAddress.ToString() == "192.168.0.0");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Override_Default_Networks_When_KnownIPNetworks_Configured()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0:BaseAddress", "198.51.100.0"),
            ("ForwardedHeaders:KnownIPNetworks:0:PrefixLength", "24"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownIPNetworks.Should().ContainSingle(n => n.BaseAddress.ToString() == "198.51.100.0" && n.PrefixLength == 24);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Support_CIDR_Notation_In_KnownIPNetworks()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0", "203.0.113.0/24"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownIPNetworks.Should().ContainSingle(n => n.BaseAddress.ToString() == "203.0.113.0" && n.PrefixLength == 24);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Allow_Explicit_Private_Network_When_TrustPrivateNetworks_Is_False()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:TrustPrivateNetworks", "false"),
            ("ForwardedHeaders:KnownIPNetworks:0", "10.0.0.0/8"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownIPNetworks.Should().HaveCount(1);
        options.KnownIPNetworks.Should().ContainSingle(n => n.BaseAddress.ToString() == "10.0.0.0" && n.PrefixLength == 8);
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Support_KnownProxies_Configuration()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownProxies:0", "198.51.100.50"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        configure(options);

        options.KnownProxies.Should().ContainSingle(ip => ip.ToString() == "198.51.100.50");
    }

    [Theory]
    [DataInlineUnit("invalid-cidr")]
    [DataInlineUnit("192.168.1.1/33")]
    public void ConfigureForwardedHeadersOptions_Should_Throw_ConfigurationException_When_KnownIPNetworks_CIDR_Is_Invalid(string cidr)
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0", cidr));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("Invalid ForwardedHeaders:KnownIPNetworks configuration.");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Throw_ConfigurationException_When_KnownIPNetworks_Object_Is_Invalid()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownIPNetworks:0:BaseAddress", "invalid-ip"),
            ("ForwardedHeaders:KnownIPNetworks:0:PrefixLength", "24"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("Invalid ForwardedHeaders:KnownIPNetworks configuration.");
    }

    [Fact]
    public void ConfigureForwardedHeadersOptions_Should_Throw_ConfigurationException_When_KnownProxies_Is_Invalid()
    {
        var appSettings = CreateAppSettings(
            isDevelopment: false,
            ("ForwardedHeaders:KnownProxies:0", "invalid-proxy-ip"));
        var options = new ForwardedHeadersOptions();
        var configure = new TestProgram().ExposeConfigureForwardedHeadersOptions(appSettings);

        var act = () => configure(options);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("Invalid ForwardedHeaders:KnownProxies configuration.");
    }

    private static IAppSettings CreateAppSettings(bool isDevelopment, params (string Key, string Value)[] settings)
    {
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(isDevelopment);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
        appSettings.Configuration.Returns(config);
        appSettings.TryGetSection(Arg.Any<string>(), out Arg.Any<IConfigurationSection>())
            .Returns(callInfo =>
            {
                var key = callInfo.Arg<string>();
                var section = config.GetSection(key);
                if (section.Exists())
                {
                    callInfo[1] = section;
                    return true;
                }

                callInfo[1] = null!;
                return false;
            });

        return appSettings;
    }

    private sealed class TestProgram : DrnProgramBase<TestProgram>, IDrnProgram
    {
        public static AuthenticationClaimConfig Claims { get; } = new()
        {
            Subject = new("uid"), Name = new("display"), Email = new("mail"), Roles = new("app-role")
        };

        public static Task Main(string[] args) => Task.CompletedTask;

        public void RegisterDefaults(WebApplicationBuilder builder, IAppSettings settings) => ConfigureApplicationBuilder(builder, settings);

        public void ConfigureHeaders(HeaderPolicyCollection policies, IServiceProvider provider, IAppSettings settings) =>
            ConfigureDefaultSecurityHeaders(policies, provider, settings);

        protected override AuthenticationClaimConfig ConfigureAuthenticationClaims() => Claims;

        public Action<HostFilteringOptions> ExposeConfigureHostFilteringOptions(IAppSettings appSettings)
            => ConfigureHostFilteringOptions(appSettings);

        public Action<ForwardedHeadersOptions> ExposeConfigureForwardedHeadersOptions(IAppSettings appSettings)
            => ConfigureForwardedHeadersOptions(appSettings);

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
            => Task.CompletedTask;
    }
}
