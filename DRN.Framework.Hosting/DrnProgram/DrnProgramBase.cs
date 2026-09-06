using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Reflection;
using System.Threading.RateLimiting;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram.Configurators;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DRN.Framework.Hosting.DrnProgram;

//todo: evaluate Redis integration boundaries for distributed cache vs distributed rate limiting.
// HybridCache + Redis IDistributedCache is useful for tenant plan, feature flag, and quota policy snapshots,
// but it is not an atomic distributed quota engine. Hard cross-replica limits should use edge enforcement
// and/or a direct Redis implementation with StackExchange.Redis + Lua/atomic commands, exposed through
// RateLimitRuleResult.CustomPartition(...) or an optional DRN.Framework.Hosting.Redis companion package.
// Keep DRN.Framework.Hosting core Redis-free unless a concrete cross-cutting dependency is justified.

//todo: evaluate optional OpenTelemetry exporter wiring for DRN metrics.
// DRN currently emits Meter data only; host apps must subscribe to RateLimitTelemetry.MeterName
// and choose exporters such as OTLP, Prometheus, or Azure Monitor. Keep core exporter-agnostic;
// consider an optional AddDrnOpenTelemetry(...) extension/companion package that adds DRN meters,
// ASP.NET Core/runtime meters, safe histogram views, and no high-cardinality partition/user/IP tags.

public abstract class DrnProgram
{
    static DrnProgram() => UtilsConventionBuilder.BuildConvention();
}

public interface IDrnProgram
{
    static abstract Task Main(string[] args);
}

//todo: add cookie manager
//todo: add csp manager
//todo: review page for and endpoint for
//todo: unify reports - startup, middleware, StaticAssetWarm, endpoint list etc
//todo: add support for minimal apis (MapMinimalEndpoints, HttpJsonOptions, DrnEndpointSource discovery)
/// <summary>
/// Derive from this class to compose an application's services, security policies, and request pipeline
/// while retaining DRN hosting conventions.
/// </summary>
/// <remarks>
/// Implement <see cref="AddServicesAsync"/> for application registrations and prefer the narrow configuration
/// hooks over replacing the complete builder or pipeline. Hooks execute during startup or options resolution;
/// register middleware in pipeline hooks to perform work for each request.
/// <para>References:</para>
/// <list type="bullet">
/// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host">Generic host model</a></description></item>
/// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60">WebApplication hosting model</a></description></item>
/// <item><description><a href="https://andrewlock.net/exploring-dotnet-6-part-2-comparing-webapplicationbuilder-to-the-generic-host">Comparing WebApplicationBuilder to the generic host</a></description></item>
/// <item><description><a href="https://andrewlock.net/exploring-dotnet-6-part-3-exploring-the-code-behind-webapplicationbuilder">Code behind WebApplicationBuilder</a></description></item>
/// <item><description><a href="https://andrewlock.net/exploring-the-dotnet-8-preview-comparing-createbuilder-to-the-new-createslimbuilder-method">Comparing default and slim builders</a></description></item>
/// <item><description><a href="https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-part-1">Running async tasks at startup</a></description></item>
/// <item><description><a href="https://stackoverflow.com/questions/57846127/what-are-the-differences-between-app-userouting-and-app-useendpoints">UseRouting versus UseEndpoints</a></description></item>
/// </list>
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UseUtf8StringLiteral")]
public abstract class DrnProgramBase<TProgram> : DrnProgram
    where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    public const string NlogConfigSectionName = "NLog";

    // Keep virtual hooks here as the application contract; internal configurators own their default implementations.
    /// <summary>
    /// Access the Swagger settings shared by service registration and endpoint mapping.
    /// Customize them in <see cref="ConfigureSwaggerOptions"/> before the builder is created.
    /// </summary>
    protected DrnProgramSwaggerOptions DrnProgramSwaggerOptions { get; private set; } = new();
    /// <summary>
    /// Select a different native builder when the application needs to own its hosting setup.
    /// Set this during program construction, before builder creation.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="DrnAppBuilderType.DrnDefaults"/>. Other values skip the DRN default pipeline
    /// and the builder registrations after the builder-type guard; they do not skip all DRN registrations.
    /// Override <see cref="ConfigureApplication"/> to supply the corresponding pipeline.
    /// </remarks>
    protected DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;

    /// <summary>
    /// Customize NLog provider integration for this program type before logging is initialized.
    /// Use <see cref="ConfigureLoggingBuilder"/> to change the built host's provider selection.
    /// </summary>
    /// <remarks>
    /// Shared by instances of the same program type, independently of other program types.
    /// Both bootstrap and host logging use these options; avoid mutating them after startup.
    /// </remarks>
    // ReSharper disable once StaticMemberInGenericType
    protected static NLogAspNetCoreOptions NLogOptions { get; set; } = DrnNLogConfigurator.CreateDefaultOptions();

    private static LogFactory CreateLogFactory(IAppSettings appSettings) => DrnNLogConfigurator.CreateLogFactory(appSettings, NlogConfigSectionName);

    protected static async Task RunAsync(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddDrnSettings(GetApplicationAssemblyName(), args).Build();
        using var appSettings = new AppSettings(configuration);
        var scopedLog = new ScopedLog(appSettings).WithLoggerName(typeof(TProgram).FullName);
        await using var bootstrapLoggerProvider = new NLogLoggerProvider(NLogOptions, CreateLogFactory(appSettings));
        var logger = bootstrapLoggerProvider.CreateLogger(typeof(TProgram).FullName!);
        WebApplication? application = null;
        var disposeApplication = false;

        try
        {
            scopedLog.AddToActions("Creating Application");
            application = await CreateApplicationAsync(args, appSettings, scopedLog);
            logger = application.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TProgram).FullName!);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.SkipValidation)}", appSettings.DevelopmentSettings.SkipValidation);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.TemporaryApplication)}", appSettings.DevelopmentSettings.TemporaryApplication);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.Prototype)}", appSettings.DevelopmentSettings.Prototype);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.AutoMigrateDevelopment)}", appSettings.DevelopmentSettings.AutoMigrateDevelopment);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.AutoMigrateStaging)}", appSettings.DevelopmentSettings.AutoMigrateStaging);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.LaunchExternalDependencies)}", appSettings.DevelopmentSettings.LaunchExternalDependencies);
            scopedLog.AddToActions("Running Application");
            disposeApplication = !appSettings.DevelopmentSettings.TemporaryApplication;
            logger.LogWarning("{@Logs}", scopedLog.GetLogs());

            if (appSettings.DevelopmentSettings.TemporaryApplication)
                return;

            var lifetime = application.Services.GetRequiredService<IHostApplicationLifetime>();
            ApplicationLifetime.ShutdownAction = lifetime.StopApplication;

            //todo create startup report for dev environment
            await application.StartAsync();
            await application.WaitForShutdownAsync();
            scopedLog.AddToActions("Application Shutdown Gracefully");
        }
        catch (Exception exception)
        {
            scopedLog.AddException(exception);
            await TryCreateStartupExceptionReport(args, appSettings, scopedLog, exception, logger);

            throw;
        }
        finally
        {
            try
            {
                if (scopedLog.HasException)
                    logger.LogError("{@Logs}", scopedLog.GetLogs());
                else
                    logger.LogWarning("{@Logs}", scopedLog.GetLogs());
            }
            finally
            {
                if (disposeApplication && application != null)
                    await application.DisposeAsync();
            }
        }
    }

    private static Task TryCreateStartupExceptionReport(string[]? args, AppSettings appSettings, IScopedLog scopedLog, Exception exception, ILogger logger) =>
        DrnStartupExceptionReporter.TryCreateReportAsync(async () =>
        {
            var (_, applicationBuilder) = await CreateApplicationBuilder(args, appSettings, scopedLog);
            return applicationBuilder;
        }, typeof(TProgram).Assembly, appSettings, scopedLog, exception, logger);

    public static Task<WebApplication> CreateApplicationAsync( string[]? args, IAppSettings appSettings, IScopedLog scopeLog)
        => CreateApplicationAsync(args, appSettings, scopeLog, null);

    public static async Task<WebApplication> CreateApplicationAsync(
        string[]? args,
        IAppSettings appSettings,
        IScopedLog scopeLog,
        Action<WebApplicationBuilder>? configureBuilder)
    {
        var actions = GetApplicationAssembly().CreateSubType<DrnProgramActions>();
        var (program, applicationBuilder) = await CreateApplicationBuilder(args, appSettings, scopeLog, configureBuilder);
        await (actions?.ApplicationBuilderCreatedAsync(program, applicationBuilder, appSettings, scopeLog) ?? Task.CompletedTask);

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application, appSettings);
        await (actions?.ApplicationBuiltAsync(program, application, appSettings, scopeLog) ?? Task.CompletedTask);

        var requestPipelineSummary = application.GetRequestPipelineSummary();
        if (appSettings.IsDevelopmentEnvironment) //todo send application summaries to nexus for auditing, implement application dependency summary as well
            scopeLog.Add(nameof(RequestPipelineSummary), requestPipelineSummary);

        program.ValidateEndpoints(application, appSettings);
        await program.ValidateServicesAsync(application, scopeLog);
        await (actions?.ApplicationValidatedAsync(program, application, appSettings, scopeLog) ?? Task.CompletedTask);

        return application;
    }

    private static async Task<(TProgram program, WebApplicationBuilder applicationBuilder)> CreateApplicationBuilder(
        string[]? args,
        IAppSettings appSettings,
        IScopedLog scopeLog,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var program = new TProgram();
        var options = new WebApplicationOptions
        {
            Args = args,
            ApplicationName = GetApplicationAssemblyName(),
            EnvironmentName = appSettings.Environment.ToString()
        };
        program.ConfigureSwaggerOptions(program.DrnProgramSwaggerOptions, appSettings);

        var applicationBuilder = DrnProgramConventions.GetApplicationBuilder<TProgram>(options, program.AppBuilderType);
        applicationBuilder.Configuration.AddDrnSettings(GetApplicationAssemblyName(), args);

        program.ConfigureApplicationBuilder(applicationBuilder, appSettings);
        await program.AddServicesAsync(applicationBuilder, appSettings, scopeLog);
        configureBuilder?.Invoke(applicationBuilder);
        return (program, applicationBuilder);
    }

    /// <summary>
    /// Register application modules, authentication schemes, and application-specific service options.
    /// </summary>
    /// <remarks>
    /// Runs after <see cref="ConfigureApplicationBuilder"/> and before the optional external builder callback.
    /// Await required registration work here; the application service provider has not yet been built.
    /// </remarks>
    protected abstract Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog);

    /// <summary>
    /// Override to change how the hosting subsystems are registered when the narrower configuration hooks are insufficient.
    /// </summary>
    /// <remarks>
    /// Call base to retain DRN service, MVC, authorization, and default options wiring.
    /// Prefer <see cref="AddServicesAsync"/> for application registrations; it runs after this method.
    /// Omitting base makes the override responsible for services needed by the chosen pipeline.
    /// </remarks>
    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        ConfigureLoggingBuilder(appSettings, applicationBuilder.Logging);
        ConfigureWebHostBuilder(appSettings, applicationBuilder.WebHost);

        var services = applicationBuilder.Services;
        services.AddSingleton(ConfigureAuthenticationClaims());
        services.AddOptions<IdentityOptions>().PostConfigure<AuthenticationClaimConfig>(DrnSecurityConfigurator.ConfigureIdentityClaims);
        services.AddDrnHosting(DrnProgramSwaggerOptions, appSettings.Configuration);
        services.AddSingleton<IEndpointAccessor>(sp =>
        {
            var endpointHelper = sp.GetRequiredService<IEndpointHelper>();
            var endpoints = EndpointCollectionBase<TProgram>.Endpoints;
            var pageEndpoints = EndpointCollectionBase<TProgram>.PageEndpoints;
            var apiEndpoints = EndpointCollectionBase<TProgram>.ApiEndpoints;
            return new EndpointAccessor(endpointHelper, endpoints, apiEndpoints, pageEndpoints, typeof(TProgram));
        });

        services.AddResponseCaching(ConfigureResponseCachingOptions);
        services.AddResponseCompression(ConfigureResponseCompressionOptions);
        ConfigureCompressionProviders(services);
        var mvcBuilder = services.AddMvc(ConfigureMvcOptions);
        ConfigureMvcBuilder(mvcBuilder, appSettings);

        DrnSecurityConfigurator.AddAntiforgery(services, appSettings);

        services.AddAuthorization(ConfigureAuthorizationOptions);
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        if (!appSettings.Features.RateLimit.Disabled)
        {
            services.AddRateLimiter();
            // Intentionally manual: preserves the virtual CreatePreAuthRateLimiter extension point.
            services.AddSingleton(sp => new DrnPreAuthRateLimiter(CreatePreAuthRateLimiter(sp, appSettings)));
        }

        services.Configure<CookiePolicyOptions>(options => ConfigureCookiePolicy(options, appSettings));
        services.Configure<CookieTempDataProviderOptions>(options => ConfigureCookieTempDataProvider(options, appSettings));
        services.Configure(ConfigureStaticFileOptions(appSettings));
        services.Configure(ConfigureForwardedHeadersOptions(appSettings));
        ConfigureIdentityRenewal(services, appSettings);
        if (appSettings.Localization.Enabled)
            services.Configure(ConfigureRequestLocalizationOptions(appSettings));

        services.PostConfigure(ConfigureHostFilteringOptions(appSettings));

        services.AddSecurityHeaderPolicies((builder, provider) =>
        {
            var policyCollection = new HeaderPolicyCollection();
            ConfigureDefaultSecurityHeaders(policyCollection, provider, appSettings);
            builder.SetDefaultPolicy(policyCollection);
            ConfigureSecurityHeaderPolicyBuilder(builder, provider, appSettings);
        });
    }

    /// <summary>
    /// Override to add or replace the built host's logging providers, filters, or telemetry integration.
    /// </summary>
    /// <remarks>
    /// Base clears providers and adds NLog when its section exists; call it before adding providers to retain them.
    /// This hook does not configure the standalone bootstrap provider used by <see cref="RunAsync"/>.
    /// </remarks>
    protected virtual void ConfigureLoggingBuilder(IAppSettings appSettings, ILoggingBuilder loggingBuilder) =>
        DrnNLogConfigurator.ConfigureLoggingBuilder(appSettings, loggingBuilder, NlogConfigSectionName, NLogOptions);

    /// <summary>
    /// Override to customize server listeners, transport limits, or web-host integration before the application is built.
    /// </summary>
    /// <remarks>
    /// Base configures Kestrel from settings, suppresses its server header, and enables static web assets.
    /// Configure the supplied builder; the caller does not consume a replacement return value.
    /// </remarks>
    protected virtual IWebHostBuilder ConfigureWebHostBuilder(IAppSettings appSettings, ConfigureWebHostBuilder webHostBuilder) =>
        DrnWebHostConfigurator.ConfigureWebHostBuilder(appSettings, webHostBuilder);

    /// <summary>
    /// Override to own the complete middleware pipeline, such as when using a non-DRN builder mode.
    /// Prefer individual pipeline-stage hooks when retaining the standard ordering.
    /// </summary>
    /// <remarks>
    /// Base composes stages and maps endpoints only in <see cref="DrnAppBuilderType.DrnDefaults"/> mode.
    /// Omitting base transfers responsibility for security, request scopes, routing, and endpoint mapping to the override.
    /// </remarks>
    protected virtual void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        var pipeline = new DrnPipelineConfigurator
        {
            PipelineStart = ConfigureApplicationPipelineStart,
            PreScopeStart = ConfigureApplicationPreScopeStart,
            PostScopeStart = ConfigureApplicationPostScopeStart,
            PreAuthentication = ConfigureApplicationPreAuthentication,
            PostAuthentication = ConfigureApplicationPostAuthentication,
            PostAuthorization = ConfigureApplicationPostAuthorization,
            MapEndpoints = MapApplicationEndpoints,
            CreatePostAuthRateLimiterOptions = CreatePostAuthRateLimiterOptions
        };
        pipeline.Configure(application, appSettings);
    }

    /// <summary>
    /// Override to adapt cross-origin isolation, framing, permissions, or custom headers to the application's browser integrations.
    /// </summary>
    /// <remarks>
    /// Call base before applying targeted changes to retain the other protections. This hook builds the default
    /// and named CSP policies; it can run multiple times and is not a per-request callback.
    /// Named policies replace its CSP afterward; use <see cref="ConfigureDefaultCspBase"/> for shared CSP directives.
    /// <para>References and validation tools:</para>
    /// <list type="bullet">
    /// <item><description><a href="https://securityheaders.com/">Security Headers scanner</a></description></item>
    /// <item><description><a href="https://csp-evaluator.withgoogle.com">CSP Evaluator</a></description></item>
    /// <item><description><a href="https://www.nuget.org/packages/NetEscapades.AspNetCore.SecurityHeaders">SecurityHeaders package</a></description></item>
    /// <item><description><a href="https://andrewlock.net/major-updates-to-netescapades-aspnetcore-security-headers/">SecurityHeaders package updates</a></description></item>
    /// <item><description><a href="https://andrewlock.net/series/understanding-cross-origin-security-headers">Understanding cross-origin security headers</a></description></item>
    /// <item><description><a href="https://mvsp.dev/">Minimum Viable Secure Product checklist</a></description></item>
    /// </list>
    /// </remarks>
    protected virtual void ConfigureDefaultSecurityHeaders(HeaderPolicyCollection policies, IServiceProvider serviceProvider, IAppSettings appSettings) =>
        DrnSecurityConfigurator.ConfigureDefaultSecurityHeaders(policies, appSettings, ConfigureDefaultCsp);

    /// <summary>
    /// Override to customize the default nonce-based CSP, for example to support an application's script delivery model.
    /// </summary>
    /// <remarks>
    /// Base calls <see cref="ConfigureDefaultCspBase"/> and adds a script nonce. Retain nonce protection when composing
    /// changes. Named self/inline policies replace this CSP; shared directives belong in <see cref="ConfigureDefaultCspBase"/>.
    /// <para>References and validation tools:</para>
    /// <list type="bullet">
    /// <item><description><a href="https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP#strict_csp">Strict CSP</a></description></item>
    /// <item><description><a href="https://dl.acm.org/doi/pdf/10.1145/2976749.2978363">CSP research paper</a></description></item>
    /// <item><description><a href="https://www.netlify.com/blog/general-availability-content-security-policy-csp-nonce-integration/">CSP nonce integration</a></description></item>
    /// <item><description><a href="https://developer.mozilla.org/en-US/docs/Web/HTML/Global_attributes/nonce">HTML nonce attribute</a></description></item>
    /// <item><description><a href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy/default-src">CSP default-src directive</a></description></item>
    /// <item><description><a href="https://securityheaders.com/">Security Headers scanner</a></description></item>
    /// <item><description><a href="https://csp-evaluator.withgoogle.com">CSP Evaluator</a></description></item>
    /// </list>
    /// </remarks>
    protected virtual void ConfigureDefaultCsp(CspBuilder builder) =>
        DrnSecurityConfigurator.ConfigureDefaultCsp(builder, ConfigureDefaultCspBase);

    /// <summary>
    /// Override to allow application resource origins, such as image or API hosts, across the default and named CSP policies.
    /// </summary>
    /// <remarks>
    /// Call base to retain restrictive defaults, then add only required origins. Script-source selection is applied
    /// afterward by the default or named policy; this hook supplies their common directives.
    /// </remarks>
    protected virtual void ConfigureDefaultCspBase(CspBuilder builder) =>
        DrnSecurityConfigurator.ConfigureDefaultCspBase(builder);

    /// <summary>
    /// Override to register additional named header policies or select policies for application-specific routes.
    /// </summary>
    /// <remarks>
    /// Call base to retain DRN's self/inline policies and selector. Replacing the selector must also account for
    /// Swagger and endpoint-selected policies if those behaviors are still needed.
    /// </remarks>
    protected virtual void ConfigureSecurityHeaderPolicyBuilder(SecurityHeaderPolicyBuilder builder, IServiceProvider serviceProvider, IAppSettings appSettings) =>
        DrnSecurityConfigurator.ConfigureSecurityHeaderPolicyBuilder(builder, serviceProvider, appSettings,
            ConfigureDefaultSecurityHeaders, ConfigureDefaultCspBase);

    /// <summary>
    /// Override to adapt consent handling and cookie transport or SameSite rules to the application's browser flows.
    /// </summary>
    /// <remarks>
    /// Call base before targeted changes. These settings affect cookies passing through the cookie-policy middleware,
    /// so assess authentication and cross-site sign-in flows as well as the consent cookie. Runs during options resolution.
    /// </remarks>
    protected virtual void ConfigureCookiePolicy(CookiePolicyOptions options, IAppSettings appSettings) =>
        DrnSecurityConfigurator.ConfigureCookiePolicy(options, appSettings);

    /// <summary>
    /// Override to customize the TempData cookie's name, scope, or transport settings for MVC and Razor redirect flows.
    /// </summary>
    /// <remarks>Call base to retain HttpOnly and essential-cookie defaults. Runs during options resolution.</remarks>
    protected virtual void ConfigureCookieTempDataProvider(CookieTempDataProviderOptions options, IAppSettings appSettings) =>
        DrnSecurityConfigurator.ConfigureCookieTempDataProvider(options);

    /// <summary>
    /// Override to control whether Identity security-stamp renewal wiring is registered for the application's authentication mechanisms.
    /// </summary>
    /// <remarks>
    /// Call base when Identity cookie renewal is used. Prefer <see cref="ConfigureSecurityStampValidatorOptions"/>
    /// for interval or callback changes; this registration defers that hook until options resolution.
    /// Omitting this wiring is appropriate only when the mechanism is unused.
    /// </remarks>
    protected virtual void ConfigureIdentityRenewal(IServiceCollection services, IAppSettings appSettings) =>
        services.AddOptions<SecurityStampValidatorOptions>().PostConfigure<AuthenticationClaimConfig>((options, claims) =>
            ConfigureSecurityStampValidatorOptions(options, appSettings, claims));

    /// <summary>
    /// Override to change the security-stamp validation interval or compose application-specific renewal callbacks.
    /// </summary>
    /// <remarks>
    /// Set a callback before calling base so DRN wraps and awaits it while preserving a snapshot of the original
    /// account-bound evidence. Replacing the callback after base discards that protection. Scalar options can be changed afterward.
    /// </remarks>
    protected virtual void ConfigureSecurityStampValidatorOptions(
        SecurityStampValidatorOptions options,
        IAppSettings appSettings,
        AuthenticationClaimConfig claims) =>
        DrnSecurityConfigurator.ConfigureSecurityStampValidatorOptions(options, claims);

    /// <summary>
    /// Override to customize the public static-file source, request path, content types, or cache headers.
    /// </summary>
    /// <remarks>
    /// Return a delegate that invokes the base delegate before applying targeted changes. It runs during options resolution.
    /// Base enables HTTPS compression and one-year public caching; only expose files suitable for anonymous public access
    /// because this middleware runs before authentication and authorization. Replacing OnPrepareResponse replaces its cache headers.
    /// <para>References:</para>
    /// <list type="bullet">
    /// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files">Static files in ASP.NET Core</a></description></item>
    /// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression#compression-with-https">Compression with HTTPS</a></description></item>
    /// </list>
    /// </remarks>
    protected virtual Action<StaticFileOptions> ConfigureStaticFileOptions(IAppSettings appSettings) =>
        DrnCompressionConfigurator.ConfigureStaticFileOptions;

    /// <summary>
    /// Override when the deployment needs proxy trust or forwarded-header rules that configuration alone cannot express.
    /// </summary>
    /// <remarks>
    /// Return a delegate that invokes the base delegate first, then adjusts the final trust lists and hop limit.
    /// <para>
    /// <b>Cloud &amp; Kubernetes Rationale:</b>
    /// In containerized and cloud environments (e.g. Kubernetes pod CIDRs, Docker networks, cloud VPCs), Kubernetes Gateway API
    /// implementations, cloud load balancers, and service mesh sidecars or inter-service reverse proxies (such as Linkerd or Envoy)
    /// communicate across dynamic RFC 1918 private subnets (<c>10.0.0.0/8</c>, <c>172.16.0.0/12</c>, <c>192.168.0.0/16</c>).
    /// Restricting default trust strictly to loopback (<c>127.0.0.0/8</c>, <c>::1/128</c>) would reject these gateway and mesh proxies,
    /// attributing all client requests to the gateway or Linkerd proxy pod IP and starving pre-auth rate limiting.
    /// Therefore, DRN defaults to trusting RFC 1918 private subnets alongside loopback with <c>ForwardLimit = 2</c>.
    /// </para>
    /// <para>
    /// For zero-trust environments where private subnets should not be trusted by default, configure <c>ForwardedHeaders:TrustPrivateNetworks = false</c>
    /// to retain only loopback networks. A nonempty <c>ForwardedHeaders:KnownIPNetworks</c> list replaces network defaults.
    /// <c>ForwardedHeaders:KnownProxies</c> adds entries without clearing existing trust.
    /// For an exact allowlist, clear both collections in the options override before adding trusted entries.
    /// </para>
    /// </remarks>
    /// <param name="appSettings">Application configuration settings.</param>
    /// <returns>An action delegate configuring <see cref="ForwardedHeadersOptions"/>.</returns>
    protected virtual Action<ForwardedHeadersOptions> ConfigureForwardedHeadersOptions(IAppSettings appSettings) =>
        DrnRequestConfigurator.ConfigureForwardedHeadersOptions(appSettings);

    /// <summary>
    /// Override to change culture-provider precedence or supported cultures, such as selecting culture from an application-specific cookie.
    /// </summary>
    /// <remarks>
    /// Registered only when localization is enabled. Return a delegate that composes with base during options resolution;
    /// base orders providers as cookie, query string, then Accept-Language. Localization runs before authentication.
    /// </remarks>
    protected virtual Action<RequestLocalizationOptions> ConfigureRequestLocalizationOptions(IAppSettings appSettings) =>
        DrnRequestConfigurator.ConfigureRequestLocalizationOptions(appSettings);

    /// <summary>
    /// Override to supply application-specific allowed hosts, for example an allowlist derived from deployment settings.
    /// </summary>
    /// <remarks>
    /// The returned delegate runs during options post-configuration. Set a custom allowlist before invoking the base delegate
    /// so it validates the result. Base rejects missing hosts and the unrestricted '*' host outside Development.
    /// </remarks>
    protected virtual Action<HostFilteringOptions> ConfigureHostFilteringOptions(IAppSettings appSettings) =>
        DrnRequestConfigurator.ConfigureHostFilteringOptions(appSettings);

    /// <summary>
    /// Override to register early request normalization or response-header middleware that must also cover static-file requests.
    /// </summary>
    /// <remarks>
    /// Base installations forwarded headers, host filtering, cookie policy, and security headers in that order.
    /// Call base before middleware that relies on the corrected client address or host. DRN request logging and authentication
    /// have not yet run, so use later stages for scoped diagnostics or identity-dependent work.
    /// </remarks>
    protected virtual void ConfigureApplicationPipelineStart(WebApplication application, IAppSettings appSettings) =>
        DrnPipelineConfigurator.ConfigurePipelineStart(application);

    /// <summary>
    /// Override to serve public responses that can bypass the DRN request scope, such as additional static assets.
    /// </summary>
    /// <remarks>
    /// Base registers response caching, compression, and static files. Middleware added after base will not see requests
    /// already served by static files. Responses completed here bypass scoped logging, rate limiting, authentication, and authorization.
    /// </remarks>
    protected virtual void ConfigureApplicationPreScopeStart(WebApplication application, IAppSettings appSettings) =>
        DrnPipelineConfigurator.ConfigurePreScopeStart(application);

    /// <summary>
    /// Override to add request diagnostics or enrichment that needs the DRN HTTP scope but does not need route or identity information.
    /// </summary>
    /// <remarks>Base is empty. Middleware registered here runs inside HttpScopeMiddleware, before routing and authentication.</remarks>
    protected virtual void ConfigureApplicationPostScopeStart(WebApplication application, IAppSettings appSettings)
    {
    }

    /// <summary>
    /// Override to prepare routed requests before authentication, for example to establish culture used by sign-in responses.
    /// </summary>
    /// <remarks>
    /// Runs after routing and pre-auth rate limiting. Call base to retain enabled localization; authenticated user projection
    /// is not available yet. Register request work as middleware rather than resolving request services during startup.
    /// </remarks>
    protected virtual void ConfigureApplicationPreAuthentication(WebApplication application, IAppSettings appSettings) =>
        DrnPipelineConfigurator.ConfigurePreAuthentication(application, appSettings);

    /// <summary>
    /// Override to add identity-aware middleware before authorization, such as authenticated request enrichment.
    /// </summary>
    /// <remarks>
    /// Runs after authentication, scoped-user projection, and post-auth rate limiting. Call base to retain configured MFA
    /// exemption/redirection middleware; their default null hooks add none. Authorization may select another scheme later,
    /// so this stage must not substitute its current identity for the final authorization decision.
    /// </remarks>
    protected virtual void ConfigureApplicationPostAuthentication(WebApplication application, IAppSettings appSettings) =>
        DrnSecurityConfigurator.ConfigureMfa(application, ConfigureMFAExemption, ConfigureMFARedirection);

    /// <summary>
    /// Override to add middleware that should run only after authorization allows a request to continue.
    /// </summary>
    /// <remarks>
    /// Call base to retain enabled Swagger endpoints and UI. Anonymous endpoints can also reach this stage;
    /// its position does not imply that every request is authenticated. Endpoint mapping follows this hook.
    /// </remarks>
    protected virtual void ConfigureApplicationPostAuthorization(WebApplication application, IAppSettings appSettings) =>
        DrnPipelineConfigurator.ConfigurePostAuthorization(application, DrnProgramSwaggerOptions);

    //todo: evaluate and add native support for Minimal APIs in DrnProgramBase:
    // - MapMinimalEndpoints(WebApplication application, IAppSettings appSettings) extension point
    // - Configure HttpJsonOptions with JsonConventions.SetHtmlSafeWebJsonDefaults
    // - Extend EndpointCollectionBase<TProgram> and DrnEndpointSource to discover and index RouteEndpoints with MethodInfo/Delegate metadata
    // - Ensure MFA requirements and authorization policies seamlessly evaluate minimal endpoint metadata
    /// <summary>
    /// Override to add application routes, such as health checks or minimal endpoints, alongside MVC and Razor Pages.
    /// </summary>
    /// <remarks>
    /// Call base to retain controller and Razor Page mapping. Apply authorization metadata explicitly where needed;
    /// routes without it use the fallback policy. DRN's typed endpoint discovery does not yet provide full minimal-API support.
    /// </remarks>
    protected virtual void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings) =>
        DrnPipelineConfigurator.MapApplicationEndpoints(application);

    /// <summary>
    /// Override to replace the pre-auth limiter when registered singleton rules cannot express the required partitioning or algorithm.
    /// </summary>
    /// <remarks>
    /// Creates the <see cref="PartitionedRateLimiter{TResource}"/> used by
    /// <see cref="PreAuthRateLimitingMiddleware"/>. Only singleton rules are evaluated at this phase;
    /// matching rule partitions are composed with the native <c>PartitionedRateLimiter.CreateChained</c> API.
    /// <para><b>NAT/CDN WARNING:</b> If your application is behind a NAT or CDN, multiple legitimate users
    /// may share the same <c>RemoteIpAddress</c>. The pre-auth layer uses IP-based partitioning by default,
    /// with B2B-friendly coarse limits. In such deployments, consider configuring higher limits for the
    /// pre-auth layer or creating custom <see cref="ISingletonRateLimitRule"/> implementations that partition by a
    /// trusted header (e.g., <c>CF-Connecting-IP</c>, <c>X-Forwarded-For</c>) if securely provided.</para>
    /// <para>Called through a singleton registration with the root service provider. Do not capture scoped services;
    /// use singleton rules for ordinary customizations and reserve identity-dependent rules for the post-auth phase.</para>
    /// </remarks>
    protected virtual PartitionedRateLimiter<HttpContext> CreatePreAuthRateLimiter(IServiceProvider serviceProvider, IAppSettings appSettings)
        => DrnRateLimitConfigurator.CreatePreAuthRateLimiter(serviceProvider);

    /// <summary>
    /// Override to replace how post-auth limiter options are obtained; prefer <see cref="ConfigurePostAuthRateLimiterOptions"/>
    /// to customize existing options while retaining DI-registered policies.
    /// </summary>
    /// <remarks>
    /// Creates the standard .NET <see cref="RateLimiterOptions"/> used by the post-auth rate limiter
    /// (<c>UseRateLimiter()</c>). Placed after <c>ScopedUserMiddleware</c> — user identity is available
    /// for partitioning.
    /// <para>
    /// The default configuration composes all matching singleton and scoped rules into a native chained limiter,
    /// enabling B2B dimensions such as tenant + user + IP without custom acquisition logic. Scoped rules run
    /// only in this post-auth phase after <c>ScopedUserMiddleware</c>.
    /// </para>
    /// <para>Starts from the DI-configured <see cref="RateLimiterOptions"/>, so policies added with
    /// <c>builder.Services.AddRateLimiter(options =&gt; ...)</c> remain available to endpoint metadata such as
    /// <c>[EnableRateLimiting("strict")]</c>.</para>
    /// <para><b>Example — adding a named rate-limit policy through the narrower hook:</b></para>
    /// <code>
    /// protected override void ConfigurePostAuthRateLimiterOptions(
    ///     RateLimiterOptions options,
    ///     IServiceProvider serviceProvider,
    ///     IAppSettings appSettings)
    /// {
    ///     base.ConfigurePostAuthRateLimiterOptions(options, serviceProvider, appSettings);
    ///     options.AddTokenBucketLimiter("strict", opt =&gt;
    ///     {
    ///         opt.TokenLimit = 10;
    ///         opt.ReplenishmentPeriod = TimeSpan.FromSeconds(60);
    ///         opt.TokensPerPeriod = 10;
    ///         opt.QueueLimit = 0;
    ///     });
    /// }
    /// </code>
    /// <para>Static files are served by <c>UseStaticFiles()</c> before routing and are automatically
    /// exempt from rate limiting.</para>
    /// <para>Config changes (<c>DrnRateLimit.TokenLimit</c>, etc.) require application restart —
    /// <see cref="DrnAppFeatures"/> is bound as a singleton snapshot via <c>[Config]</c>.</para>
    /// </remarks>
    protected virtual RateLimiterOptions CreatePostAuthRateLimiterOptions(IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        var options = serviceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        ConfigurePostAuthRateLimiterOptions(options, serviceProvider, appSettings);
        return options;
    }

    /// <summary>
    /// Override to add named rate-limit policies, choose a rejection status, or customize post-auth rejection handling.
    /// </summary>
    /// <remarks>
    /// Base replaces GlobalLimiter with the DRN rule chain and wraps an existing OnRejected callback with telemetry and rule callbacks.
    /// Set a callback before base to retain that wrapper; set an intentional 503 status after base, which otherwise changes 503 to 429.
    /// Resolve request-scoped services from the rejection context, not the supplied root provider.
    /// </remarks>
    protected virtual void ConfigurePostAuthRateLimiterOptions(RateLimiterOptions options, IServiceProvider serviceProvider, IAppSettings appSettings) =>
        DrnRateLimitConfigurator.ConfigurePostAuthRateLimiterOptions(options, serviceProvider);

    /// <summary>
    /// Override to opt a browser application into MFA enrollment and challenge page navigation.
    /// </summary>
    /// <remarks>
    /// Return null to omit the redirection middleware, for example for an API using challenge/forbid responses.
    /// Supply setup, challenge, login, and logout URLs plus the page allowlist to redirect.
    /// This hook configures navigation; it neither registers authentication schemes nor replaces endpoint authorization.
    /// </remarks>
    protected virtual MfaRedirectionConfig? ConfigureMFARedirection() => null;

    /// <summary>
    /// Override to identify authentication schemes eligible to supply MFA exemption evidence, such as a dedicated service credential scheme.
    /// </summary>
    /// <remarks>
    /// Returning null omits exemption middleware. Keep exemptions narrowly scoped; they are security decisions, not a substitute
    /// for registering authentication or authorizing endpoints. Shared policy-selected proof validation still applies.
    /// </remarks>
    protected virtual MfaExemptionConfig? ConfigureMFAExemption() => null;

    /// <summary>
    /// Override to map an identity provider's subject, name, email, role, and completed-MFA claims into DRN's shared claim contract.
    /// </summary>
    /// <remarks>
    /// Returns a singleton configuration before application service registration. Defaults follow Identity claim types
    /// with the amr=mfa marker. Configure explicit aliases here; changing Identity claim options independently would diverge
    /// from consumers that use this contract. Authentication handlers must still issue trusted evidence.
    /// </remarks>
    protected virtual AuthenticationClaimConfig ConfigureAuthenticationClaims() => AuthenticationClaimConfig.Default;

    /// <summary>
    /// Override to add application authorization policies, such as role or permission requirements, while retaining the MFA baseline.
    /// </summary>
    /// <param name="options">The <see cref="AuthorizationOptions"/> to configure.</param>
    /// <remarks>
    /// Call base before adding policies to retain DRN's named, default, and fallback MFA policies.
    /// This hook does not register authentication schemes or replace DRN's shared MFA result-handler checks.
    /// With default behavior, this method enforces MFA and performs the following actions:
    /// <ul>
    ///   <li>Adds the <c>MFA</c> policy.</li>
    ///   <li>Adds the <c>MFAExempt</c> policy.</li>
    ///   <li>Sets the default policy to the <c>MFA</c> policy.</li>
    ///   <li>Sets the fallback policy to the <c>MFA</c> policy to enforce MFA on unauthenticated or unhandled requests.</li>
    /// </ul>
    /// </remarks>
    protected virtual void ConfigureAuthorizationOptions(AuthorizationOptions options) =>
        DrnSecurityConfigurator.ConfigureAuthorizationOptions(options);

    /// <summary>
    /// Override to tune response-cache memory limits or path matching for the application's public response workload.
    /// </summary>
    /// <remarks>
    /// Call base before targeted changes to retain its 16 MB maximum body size and case-insensitive paths.
    /// These options do not make a response cacheable; response headers and middleware eligibility still govern storage.
    /// Do not mark personalized or sensitive responses public merely to enable caching.
    /// </remarks>
    protected virtual void ConfigureResponseCachingOptions(ResponseCachingOptions options) =>
        DrnCompressionConfigurator.ConfigureResponseCachingOptions(options);

    /// <summary>
    /// Override to customize compressible MIME types or compression providers for the application's response formats.
    /// </summary>
    /// <remarks>
    /// Call base to retain Brotli/Gzip registration and the exclusion of dynamic HTTPS responses for BREACH mitigation.
    /// Public static files opt into HTTPS compression separately through <see cref="ConfigureStaticFileOptions"/>.
    /// Prefer the compression-level hooks for CPU versus transfer-size tuning.
    /// <para>References:</para>
    /// <list type="bullet">
    /// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression">Response compression in ASP.NET Core</a></description></item>
    /// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression#compression-with-https">Compression with HTTPS (BREACH/CRIME)</a></description></item>
    /// <item><description><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/caching/middleware">Response Caching Middleware</a></description></item>
    /// <item><description><a href="https://en.wikipedia.org/wiki/BREACH">BREACH attack</a></description></item>
    /// </list>
    /// </remarks>
    protected virtual void ConfigureResponseCompressionOptions(ResponseCompressionOptions options) =>
        DrnCompressionConfigurator.ConfigureResponseCompressionOptions(options);

    /// <summary>
    /// Override when compression providers need additional service or options registration.
    /// Override <see cref="ConfigureBrotliCompressionLevel"/> or <see cref="ConfigureGzipCompressionLevel"/>
    /// to customize compression levels for specific workloads.
    /// </summary>
    /// <remarks>Call base to retain deferred Brotli and Gzip level callbacks; they run when their options are resolved.</remarks>
    protected virtual void ConfigureCompressionProviders(IServiceCollection services) =>
        DrnCompressionConfigurator.ConfigureCompressionProviders(services, ConfigureBrotliCompressionLevel, ConfigureGzipCompressionLevel);

    /// <summary>
    /// Override to trade Brotli compression CPU and response latency against transfer size for the application's workload.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="CompressionLevel.SmallestSize"/> and is evaluated when provider options are resolved.
    /// Cache hits can avoid compression work, but cache misses and eviction still incur it; choose a level using workload measurements.
    /// </remarks>
    protected virtual CompressionLevel ConfigureBrotliCompressionLevel() => CompressionLevel.SmallestSize;

    /// <summary>
    /// Override to tune compression cost for clients negotiating Gzip independently of the Brotli setting.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="CompressionLevel.SmallestSize"/> and is evaluated when provider options are resolved.
    /// Consider latency and CPU on uncached responses as well as transfer size.
    /// </remarks>
    protected virtual CompressionLevel ConfigureGzipCompressionLevel() => CompressionLevel.SmallestSize;

    /// <summary>
    /// Override to add global MVC filters, model-binding rules, formatters, or validation conventions.
    /// </summary>
    /// <remarks>
    /// Base is empty. Invoked during MVC options resolution; use <see cref="ConfigureMvcBuilder"/> for application parts
    /// and builder extensions, or <see cref="MapApplicationEndpoints"/> for routes.
    /// </remarks>
    protected virtual void ConfigureMvcOptions(MvcOptions options)
    {
    }

    /// <summary>
    /// Override to include additional controller assemblies or apply MVC builder extensions and JSON options.
    /// </summary>
    /// <remarks>
    /// Call base to retain program assembly discovery, controllers as services, and HTML-safe JSON defaults.
    /// Apply additional configuration afterward and preserve safe encoding when customizing serialization.
    /// </remarks>
    protected virtual void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings) =>
        DrnMvcConfigurator.ConfigureMvcBuilder(mvcBuilder, typeof(TProgram).Assembly, typeof(TProgram).GetAssemblyName());

    /// <summary>
    /// Override to choose where Swagger is enabled and customize API metadata, document generation, or UI settings.
    /// </summary>
    /// <remarks>
    /// Runs before builder creation. Call base before changes to start with the application title and Development-only enablement.
    /// The resulting options are shared by registration and pipeline mapping; enabling Swagger does not itself define access policy.
    /// </remarks>
    protected virtual void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        options.OpenApiInfo.Title = appSettings.ApplicationName;
        options.AddSwagger = appSettings.IsDevelopmentEnvironment;
    }

    /// <summary>
    /// Override to validate mapped routes at startup, for example to reject missing endpoint metadata before serving requests.
    /// </summary>
    /// <remarks>
    /// Call base before checks that depend on DRN's typed endpoint collection; it finalizes routing data sources
    /// and binds that collection, except for temporary applications. Runs after pipeline construction and before service validation.
    /// </remarks>
    protected virtual void ValidateEndpoints(WebApplication application, IAppSettings appSettings)
    {
        if (appSettings.DevelopmentSettings.TemporaryApplication) return;

        // We don't know if user code called UseEndpoints(), so we will call it just in case, UseEndpoints() will ignore duplicate DataSources
        application.UseEndpoints(_ => { });
        var helper = application.Services.GetRequiredService<IEndpointHelper>();
        EndpointCollectionBase<TProgram>.SetEndpointDataSource(helper);
    }

    /// <summary>
    /// Override to add startup readiness checks that require the built service provider and must fail before the host starts.
    /// </summary>
    /// <remarks>
    /// Await base to retain attribute-registered service resolution and module validation, which honor SkipValidation.
    /// Create a scope for additional scoped dependencies and decide explicitly whether custom checks honor that setting too.
    /// Runs after endpoint validation; an exception aborts application creation.
    /// </remarks>
    protected virtual async Task ValidateServicesAsync(WebApplication application, IScopedLog scopeLog) =>
        await application.Services.ValidateServicesAddedByAttributesAsync(scopeLog);

    private static string GetApplicationAssemblyName() => typeof(TProgram).GetAssemblyName();
    private static Assembly GetApplicationAssembly() => typeof(TProgram).Assembly;
}
