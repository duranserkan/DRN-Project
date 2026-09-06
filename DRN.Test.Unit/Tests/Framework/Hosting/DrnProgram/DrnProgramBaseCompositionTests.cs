using System.IO.Compression;
using System.Security;
using System.Security.Claims;
using System.Text.Json;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;
using NLog.Web;

namespace DRN.Test.Unit.Tests.Framework.Hosting.DrnProgram;

public class DrnProgramBaseCompositionTests
{
    [Fact]
    public void NLogOptions_Should_Isolate_Mutations_Between_Program_Types()
    {
        var first = DefaultsProgram.LoggingOptions;
        var second = CompressionProgram.LoggingOptions;
        first.Should().NotBeSameAs(second);
        first.ReplaceLoggerFactory.Should().BeFalse();
        first.RemoveLoggerFactoryFilter.Should().BeFalse();
        second.ReplaceLoggerFactory.Should().BeFalse();
        second.RemoveLoggerFactoryFilter.Should().BeFalse();

        try
        {
            first.ReplaceLoggerFactory = true;
            first.RemoveLoggerFactoryFilter = true;

            DefaultsProgram.LoggingOptions.Should().BeSameAs(first);
            second.ReplaceLoggerFactory.Should().BeFalse();
            second.RemoveLoggerFactoryFilter.Should().BeFalse();
        }
        finally
        {
            first.ReplaceLoggerFactory = false;
            first.RemoveLoggerFactoryFilter = false;
        }
    }

    [Fact]
    public void NLogSectionName_Should_Remain_A_Public_Inherited_Constant()
    {
        const string baseSection = DrnProgramBase<DefaultsProgram>.NlogConfigSectionName;
        const string inheritedSection = DefaultsProgram.NlogConfigSectionName;

        baseSection.Should().Be("NLog");
        inheritedSection.Should().Be(baseSection);
    }

    [Fact]
    public void IdentityRenewal_Should_Defer_And_Invoke_The_Virtual_Options_Hook()
    {
        var program = new DefaultsProgram();
        var claims = new AuthenticationClaimConfig { Subject = new("uid") };
        var services = new ServiceCollection();
        services.AddSingleton(claims);
        program.RegisterRenewal(services);
        using var provider = services.BuildServiceProvider();

        program.RenewalCalls.Should().Be(0);
        var options = provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;

        program.RenewalCalls.Should().Be(1);
        program.RenewalClaims.Should().BeSameAs(claims);
        options.ValidationInterval.Should().Be(TimeSpan.FromMinutes(7));
        options.OnRefreshingPrincipal.Should().NotBeNull();
    }

    [Theory]
    [DataInlineUnit("mfa")]
    [DataInlineUnit("pwd")]
    public async Task SecurityStampRenewal_Should_Await_Previous_Callback_And_Preserve_Original_Evidence(string originalAmr)
    {
        var current = Account("user");
        current.AddClaim(new Claim("amr", originalAmr));
        current.AddClaim(new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64));
        var renewed = Account("user");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = false;
        var options = new SecurityStampValidatorOptions
        {
            OnRefreshingPrincipal = async context =>
            {
                entered = true;
                // The wrapper must already have cloned this evidence.
                current.RemoveClaim(current.FindFirst("amr")!);
                current.RemoveClaim(current.FindFirst("auth_time")!);
                current.AddClaim(new Claim("amr", originalAmr == "mfa" ? "pwd" : "mfa"));
                current.AddClaim(new Claim("auth_time", "1800000000", ClaimValueTypes.Integer64));
                await release.Task;
                context.NewPrincipal = new ClaimsPrincipal(renewed);
            }
        };
        new DefaultsProgram().ConfigureRenewal(options);
        var context = new SecurityStampRefreshingPrincipalContext
        {
            CurrentPrincipal = new ClaimsPrincipal(current),
            NewPrincipal = new ClaimsPrincipal(Account("discarded-by-callback"))
        };

        var refreshing = options.OnRefreshingPrincipal!(context);
        try
        {
            entered.Should().BeTrue();
            refreshing.IsCompleted.Should().BeFalse();
            renewed.FindAll("amr").Should().BeEmpty();
        }
        finally
        {
            release.SetResult();
            await refreshing;
        }

        context.NewPrincipal!.Identity.Should().BeSameAs(renewed);
        renewed.FindAll("amr").Should().ContainSingle().Which.Value.Should().Be(originalAmr);
        renewed.FindAll("auth_time").Should().ContainSingle().Which.Value.Should().Be("1700000000");
    }

    [Theory]
    [DataInlineUnit("missing-original")]
    [DataInlineUnit("missing-renewed")]
    [DataInlineUnit("conflicting-account")]
    [DataInlineUnit("anonymous-renewed")]
    public async Task SecurityStampRenewal_Should_Reject_Invalid_Renewed_Principals(string scenario)
    {
        var calls = 0;
        var options = new SecurityStampValidatorOptions
        {
            OnRefreshingPrincipal = context =>
            {
                calls++;
                context.NewPrincipal = scenario == "missing-renewed" ? null! : new ClaimsPrincipal(
                    Account(scenario == "conflicting-account" ? "other" : "user", scenario != "anonymous-renewed"));
                return Task.CompletedTask;
            }
        };
        new DefaultsProgram().ConfigureRenewal(options);
        var context = new SecurityStampRefreshingPrincipalContext
        {
            CurrentPrincipal = scenario == "missing-original" ? null! : new ClaimsPrincipal(Account("user")),
            NewPrincipal = new ClaimsPrincipal(Account("user"))
        };

        Func<Task> act = () => options.OnRefreshingPrincipal!(context);

        await act.Should().ThrowAsync<SecurityException>();
        calls.Should().Be(scenario == "missing-original" ? 0 : 1);
    }

    [Fact]
    public async Task SecurityStampRenewal_Should_Propagate_Previous_Callback_Failure_Without_Preserving_Evidence()
    {
        var original = Account("user");
        original.AddClaim(new Claim("amr", "mfa"));
        var renewed = Account("user");
        var failure = new InvalidOperationException("consumer renewal failure");
        var options = new SecurityStampValidatorOptions { OnRefreshingPrincipal = _ => Task.FromException(failure) };
        new DefaultsProgram().ConfigureRenewal(options);
        var context = new SecurityStampRefreshingPrincipalContext
        {
            CurrentPrincipal = new ClaimsPrincipal(original), NewPrincipal = new ClaimsPrincipal(renewed)
        };

        Func<Task> act = () => options.OnRefreshingPrincipal!(context);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        renewed.FindAll("amr").Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void MvcBuilder_Should_Preserve_Existing_Parts_And_Avoid_Duplicating_The_Program_Assembly(bool alreadyPresent)
    {
        var services = new ServiceCollection();
        var mvc = services.AddControllers();
        mvc.PartManager.ApplicationParts.Clear();
        var existing = new AssemblyPart(typeof(DrnProgramBase<>).Assembly);
        mvc.PartManager.ApplicationParts.Add(existing);
        if (alreadyPresent)
            mvc.AddApplicationPart(typeof(DefaultsProgram).Assembly);

        new DefaultsProgram().ConfigureMvc(mvc);

        mvc.PartManager.ApplicationParts.Should().Contain(existing);
        mvc.PartManager.ApplicationParts.OfType<AssemblyPart>()
            .Should().ContainSingle(part => part.Assembly == typeof(DefaultsProgram).Assembly);
    }

    [Fact]
    public void MvcBuilder_Should_Apply_HtmlSafe_Json_Defaults()
    {
        var services = new ServiceCollection();
        new DefaultsProgram().ConfigureMvc(services.AddControllers());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value;
        const string content = "<script>alert('x') & more</script>";

        var json = JsonSerializer.Serialize(new { HtmlContent = content }, options.JsonSerializerOptions);

        json.Should().Contain("\"htmlContent\":").And.Contain("\\u003C").And.Contain("\\u003E")
            .And.Contain("\\u0026").And.Contain("\\u0027");
        json.Should().NotContain("<").And.NotContain(">").And.NotContain("&").And.NotContain("'");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("htmlContent").GetString().Should().Be(content);
    }

    [Fact]
    public void Compression_Should_Exclude_Dynamic_Https_And_Enable_Static_Https()
    {
        var dynamicOptions = new ResponseCompressionOptions { EnableForHttps = true };
        var staticOptions = new StaticFileOptions { HttpsCompression = HttpsCompressionMode.DoNotCompress };

        new DefaultsProgram().ConfigureCompression(dynamicOptions, staticOptions);

        dynamicOptions.EnableForHttps.Should().BeFalse();
        staticOptions.HttpsCompression.Should().Be(HttpsCompressionMode.Compress);
    }

    [Fact]
    public void Localization_Should_Replace_Providers_In_The_Configured_Order()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DefaultCulture"] = "tr", ["SupportedCultures:0"] = "tr", ["SupportedCultures:1"] = "en"
        });
        var localization = new DrnLocalizationSettings();
        configuration.Bind(localization, options => options.BindNonPublicProperties = true);
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.Localization.Returns(localization);
        appSettings.GetAppSpecificName("Culture").Returns("_regression_Culture");
        var options = new RequestLocalizationOptions();
        var oldProvider = new CustomRequestCultureProvider(_ => Task.FromResult<ProviderCultureResult?>(null));
        options.RequestCultureProviders.Add(oldProvider);

        new DefaultsProgram().ConfigureLocalization(options, appSettings);

        options.RequestCultureProviders.Select(provider => provider.GetType()).Should().Equal(
            typeof(CookieRequestCultureProvider), typeof(QueryStringRequestCultureProvider), typeof(AcceptLanguageHeaderRequestCultureProvider));
        options.RequestCultureProviders.Should().NotContain(oldProvider);
        ((CookieRequestCultureProvider)options.RequestCultureProviders[0]).CookieName.Should().Be("_regression_Culture");
        options.DefaultRequestCulture.Culture.Name.Should().Be("tr");
        options.DefaultRequestCulture.UICulture.Name.Should().Be("tr");
        options.SupportedCultures!.Select(culture => culture.Name).Should().Equal("tr", "en");
        options.SupportedUICultures!.Select(culture => culture.Name).Should().Equal("tr", "en");
    }

    private static ClaimsIdentity Account(string subject, bool authenticated = true) =>
        new([new Claim(ClaimTypes.NameIdentifier, subject)], authenticated ? "test" : null);

    [Fact]
    public void CompressionProviders_Should_Evaluate_Each_Virtual_Level_When_Its_Options_Are_Resolved()
    {
        var program = new CompressionProgram();
        var services = new ServiceCollection();

        program.RegisterCompressionProviders(services);
        using var provider = services.BuildServiceProvider();
        program.Calls.Should().BeEmpty();

        program.BrotliLevel = CompressionLevel.Fastest;
        var brotli = provider.GetRequiredService<IOptions<BrotliCompressionProviderOptions>>().Value;

        brotli.Level.Should().Be(CompressionLevel.Fastest);
        program.Calls.Should().Equal("brotli");

        program.GzipLevel = CompressionLevel.NoCompression;
        var gzip = provider.GetRequiredService<IOptions<GzipCompressionProviderOptions>>().Value;

        gzip.Level.Should().Be(CompressionLevel.NoCompression);
        program.Calls.Should().Equal("brotli", "gzip");
    }

    [Fact]
    public void SecurityPolicies_Should_Compose_Virtual_Header_And_Csp_Hooks_For_Every_Policy()
    {
        var program = new SecurityProgram();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        var services = new ServiceCollection();
        var builder = services.AddSecurityHeaderPolicies();
        using var provider = services.BuildServiceProvider();

        program.ConfigureHeaders(new HeaderPolicyCollection(), provider, appSettings);
        program.Calls.Should().Equal("headers", "csp", "csp-base");

        program.Calls.Clear();
        program.ConfigurePolicies(builder, provider, appSettings);

        program.Calls.Should().Equal(
            "headers", "csp", "csp-base", "csp-base",
            "headers", "csp", "csp-base", "csp-base",
            "headers", "csp", "csp-base", "csp-base");
        program.Policies.Should().HaveCount(4);
        program.Policies.Should().OnlyContain(policy => policy.ContainsKey("X-Program-Customization"));
    }

    // These programs expose isolated options hooks; neither starts an application or serves as a test host.
    private sealed class DefaultsProgram : DrnProgramBase<DefaultsProgram>, IDrnProgram
    {
        public static NLogAspNetCoreOptions LoggingOptions => NLogOptions;
        private readonly IAppSettings _settings = Substitute.For<IAppSettings>();
        public int RenewalCalls { get; private set; }
        public AuthenticationClaimConfig? RenewalClaims { get; private set; }
        public static Task Main(string[] args) => Task.CompletedTask;
        public void RegisterRenewal(IServiceCollection services) => ConfigureIdentityRenewal(services, _settings);
        public void ConfigureRenewal(SecurityStampValidatorOptions options) =>
            ConfigureSecurityStampValidatorOptions(options, _settings, AuthenticationClaimConfig.Default);
        public void ConfigureMvc(IMvcBuilder builder) => ConfigureMvcBuilder(builder, _settings);
        public void ConfigureCompression(ResponseCompressionOptions dynamicOptions, StaticFileOptions staticOptions)
        {
            ConfigureResponseCompressionOptions(dynamicOptions);
            ConfigureStaticFileOptions(_settings)(staticOptions);
        }
        public void ConfigureLocalization(RequestLocalizationOptions options, IAppSettings settings) =>
            ConfigureRequestLocalizationOptions(settings)(options);
        protected override void ConfigureSecurityStampValidatorOptions(SecurityStampValidatorOptions options,
            IAppSettings appSettings, AuthenticationClaimConfig claims)
        {
            RenewalCalls++;
            RenewalClaims = claims;
            base.ConfigureSecurityStampValidatorOptions(options, appSettings, claims);
            options.ValidationInterval = TimeSpan.FromMinutes(7);
        }
        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) =>
            Task.CompletedTask;
    }

    private sealed class CompressionProgram : DrnProgramBase<CompressionProgram>, IDrnProgram
    {
        public static NLogAspNetCoreOptions LoggingOptions => NLogOptions;
        public List<string> Calls { get; } = [];
        public CompressionLevel BrotliLevel { get; set; } = CompressionLevel.SmallestSize;
        public CompressionLevel GzipLevel { get; set; } = CompressionLevel.SmallestSize;

        public static Task Main(string[] args) => Task.CompletedTask;

        public void RegisterCompressionProviders(IServiceCollection services) => ConfigureCompressionProviders(services);

        protected override CompressionLevel ConfigureBrotliCompressionLevel()
        {
            Calls.Add("brotli");
            return BrotliLevel;
        }

        protected override CompressionLevel ConfigureGzipCompressionLevel()
        {
            Calls.Add("gzip");
            return GzipLevel;
        }

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) =>
            Task.CompletedTask;
    }

    private sealed class SecurityProgram : DrnProgramBase<SecurityProgram>, IDrnProgram
    {
        public List<string> Calls { get; } = [];
        public List<HeaderPolicyCollection> Policies { get; } = [];

        public static Task Main(string[] args) => Task.CompletedTask;

        public void ConfigureHeaders(HeaderPolicyCollection policies, IServiceProvider provider, IAppSettings appSettings) =>
            ConfigureDefaultSecurityHeaders(policies, provider, appSettings);

        public void ConfigurePolicies(SecurityHeaderPolicyBuilder builder, IServiceProvider provider, IAppSettings appSettings) =>
            ConfigureSecurityHeaderPolicyBuilder(builder, provider, appSettings);

        protected override void ConfigureDefaultSecurityHeaders(HeaderPolicyCollection policies, IServiceProvider serviceProvider, IAppSettings appSettings)
        {
            Calls.Add("headers");
            base.ConfigureDefaultSecurityHeaders(policies, serviceProvider, appSettings);
            policies.AddCustomHeader("X-Program-Customization", "preserved");
            Policies.Add(policies);
        }

        protected override void ConfigureDefaultCsp(CspBuilder builder)
        {
            Calls.Add("csp");
            base.ConfigureDefaultCsp(builder);
        }

        protected override void ConfigureDefaultCspBase(CspBuilder builder)
        {
            Calls.Add("csp-base");
            base.ConfigureDefaultCspBase(builder);
        }

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) =>
            Task.CompletedTask;
    }
}
