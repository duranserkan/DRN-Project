using System.IO.Compression;
using System.Reflection;
using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Consent;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Utils;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DRN.Framework.Hosting.DrnProgram;

public abstract class DrnProgram
{
    static DrnProgram() => UtilsConventionBuilder.BuildConvention();
}

public interface IDrnProgram
{
    static abstract Task Main(string[] args);
}
//todo: add rate limit
//todo: add cookie manager
//todo: add csp manager
//todo: review page for and endpoint for
/// <summary>
/// <li><a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host">Generic host model</a></li>
/// <li><a href="https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60">WebApplication - new hosting model</a></li>
/// <li><a href="https://andrewlock.net/exploring-dotnet-6-part-2-comparing-webapplicationbuilder-to-the-generic-host">Comparing WebApplication to the generic host</a></li>
/// <li><a href="https://andrewlock.net/exploring-dotnet-6-part-3-exploring-the-code-behind-webapplicationbuilder">Code behind WebApplicationBuilder</a></li>
/// <li><a href="https://andrewlock.net/exploring-the-dotnet-8-preview-comparing-createbuilder-to-the-new-createslimbuilder-method">Comparing default builder to slim builder</a></li>
/// <li><a href="https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-part-1">Running async tasks at startup</a></li>
/// <li><a href="https://stackoverflow.com/questions/57846127/what-are-the-differences-between-app-userouting-and-app-useendpoints">UseRouting vs. UseEndpoints</a></li>
/// </summary>
public abstract class DrnProgramBase<TProgram> : DrnProgram
    where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    public const string NlogConfigSectionName = "NLog";
    protected DrnProgramSwaggerOptions DrnProgramSwaggerOptions { get; private set; } = new();
    protected DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;

    // ReSharper disable once StaticMemberInGenericType
    protected static NLogAspNetCoreOptions NLogOptions { get; set; } = new()
    {
        ReplaceLoggerFactory = false,
        RemoveLoggerFactoryFilter = false
    };

    private static LogFactory CreateLogFactory(IAppSettings appSettings)
    {
        var logFactory = new LogFactory();
        logFactory.Setup().SetupExtensions(ext =>
        {
            ext.RegisterAssembly("NLog.Extensions.Logging");
            ext.RegisterAssembly("NLog.Web.AspNetCore");
            ext.RegisterAssembly("NLog.Targets.Network");
        });

        var configuration = new NLogLoggingConfiguration(appSettings.GetRequiredSection(NlogConfigSectionName));
        logFactory.Configuration = configuration;

        return logFactory;
    }

    protected static async Task RunAsync(string[]? args = null)
    {
        var configuration = new ConfigurationBuilder().AddDrnSettings(GetApplicationAssemblyName(), args).Build();
        var appSettings = new AppSettings(configuration);
        var scopedLog = new ScopedLog(appSettings).WithLoggerName(typeof(TProgram).FullName);
        var loggerProvider = new NLogLoggerProvider(NLogOptions, CreateLogFactory(appSettings));
        var logger = loggerProvider.CreateLogger(typeof(TProgram).FullName!);

        try
        {
            scopedLog.AddToActions("Creating Application");
            var application = await CreateApplicationAsync(args, appSettings, scopedLog);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.SkipValidation)}", appSettings.DevelopmentSettings.SkipValidation);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.TemporaryApplication)}", appSettings.DevelopmentSettings.TemporaryApplication);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.Prototype)}", appSettings.DevelopmentSettings.Prototype);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.AutoMigrate)}", appSettings.DevelopmentSettings.AutoMigrate);
            scopedLog.Add($"DrnDevelopmentSettings_{nameof(DrnDevelopmentSettings.LaunchExternalDependencies)}", appSettings.DevelopmentSettings.LaunchExternalDependencies);
            scopedLog.AddToActions("Running Application");
            logger.LogWarning("{@Logs}", scopedLog.Logs);

            if (appSettings.DevelopmentSettings.TemporaryApplication)
                return;
            //todo create startup report for dev environment
            await application.RunAsync();
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
            if (scopedLog.HasException)
                logger.LogError("{@Logs}", scopedLog.Logs);
            else
                logger.LogWarning("{@Logs}", scopedLog.Logs);

            loggerProvider.Dispose();
        }
    }

    private static async Task TryCreateStartupExceptionReport(string[]? args, AppSettings appSettings, IScopedLog scopedLog, Exception exception, ILogger logger)
    {
        try
        {
            var (_, applicationBuilder) = await CreateApplicationBuilder(args, appSettings, scopedLog);
            var services = applicationBuilder.Services.BuildServiceProvider();
            var isDevelopment = services.GetService<IAppSettings>()?.IsDevEnvironment ?? false;
            var handler = services.GetService<IDrnExceptionHandler>();
            if (handler != null && isDevelopment)
            {
                var exceptionContentResult = await handler.GetExceptionContentAsync(services, exception, scopedLog);
                if (exceptionContentResult != null)
                {
                    var directory = Path.GetDirectoryName(typeof(TProgram).Assembly.Location)!;
                    var reportDirectory = Path.Combine(directory, "StartupReports");
                    var wwwRootDirectory = Path.Combine(reportDirectory, "wwwroot");
                    ResourceExtractor.CopyWwwrootResourcesToDirectory(wwwRootDirectory);

                    //since the application is down, we should serve exception page scripts from somewhere else;
                    var exceptionReportContent = exceptionContentResult.Content.Replace("/_content/DRN.Framework.Hosting", wwwRootDirectory);

                    var reportPath = Path.Combine(directory, "StartupExceptionReport.html");
                    var reportUrl = $"file://{reportPath}";
                    await File.WriteAllTextAsync(reportPath, exceptionReportContent);
                    scopedLog.Add("StartupExceptionReportPath", reportUrl);
                    logger.LogError("Startup Exception Report Path: {ReportUrl}", reportUrl);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogDebug(e, "Failed to generate startup exception report");
        }
    }

    public static async Task<WebApplication> CreateApplicationAsync(string[]? args, IAppSettings appSettings, IScopedLog scopeLog)
    {
        var actions = GetApplicationAssembly().CreateSubType<DrnProgramActions>();
        var (program, applicationBuilder) = await CreateApplicationBuilder(args, appSettings, scopeLog);
        await (actions?.ApplicationBuilderCreatedAsync(program, applicationBuilder, appSettings, scopeLog) ?? Task.CompletedTask);

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application, appSettings);
        await (actions?.ApplicationBuiltAsync(program, application, appSettings, scopeLog) ?? Task.CompletedTask);

        var requestPipelineSummary = application.GetRequestPipelineSummary();
        if (appSettings.IsDevEnvironment) //todo send application summaries to nexus for auditing, implement application dependency summary as well
            scopeLog.Add(nameof(RequestPipelineSummary), requestPipelineSummary);

        program.ValidateEndpoints(application, appSettings);
        await program.ValidateServicesAsync(application, scopeLog);
        await (actions?.ApplicationValidatedAsync(program, application, appSettings, scopeLog) ?? Task.CompletedTask);

        return application;
    }

    private static async Task<(TProgram program, WebApplicationBuilder applicationBuilder)> CreateApplicationBuilder(string[]? args, IAppSettings appSettings, IScopedLog scopeLog)
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
        return (program, applicationBuilder);
    }

    protected abstract Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog);

    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        applicationBuilder.Logging.ClearProviders();
        if (appSettings.TryGetSection("Logging", out var loggingSection))
            applicationBuilder.Logging.AddConfiguration(loggingSection);
        applicationBuilder.Logging.AddNLogWeb(CreateLogFactory(appSettings), NLogOptions);

        applicationBuilder.WebHost.UseKestrelCore().ConfigureKestrel(kestrelServerOptions =>
        {
            kestrelServerOptions.AddServerHeader = false;
            kestrelServerOptions.Configure(applicationBuilder.Configuration.GetSection("Kestrel"));
        });

        applicationBuilder.WebHost.UseStaticWebAssets();

        var services = applicationBuilder.Services;
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

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = appSettings.GetAppSpecificName("Antiforgery");
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.HttpOnly = true;
        });

        services.AddAuthorization(ConfigureAuthorizationOptions);
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        services.Configure(GetConfigureCookiePolicy(appSettings));
        services.Configure(ConfigureSecurityStampValidatorOptions(appSettings));
        services.Configure(GetConfigureCookieTempDataProvider(appSettings));
        services.Configure(ConfigureStaticFileOptions(appSettings));
        services.Configure(ConfigureForwardedHeadersOptions(appSettings));
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

    protected virtual void ConfigureApplication(WebApplication application, IAppSettings appSettings)
    {
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        ConfigureApplicationPipelineStart(application, appSettings);

        ConfigureApplicationPreScopeStart(application, appSettings);
        application.UseMiddleware<HttpScopeMiddleware>();
        ConfigureApplicationPostScopeStart(application, appSettings);

        application.UseRouting();

        ConfigureApplicationPreAuthentication(application, appSettings);
        application.UseAuthentication();
        application.UseMiddleware<ScopedUserMiddleware>();
        ConfigureApplicationPostAuthentication(application, appSettings);
        application.UseAuthorization();
        ConfigureApplicationPostAuthorization(application, appSettings);

        MapApplicationEndpoints(application, appSettings);
    }

    /// <summary>
    /// Configures security headers that are added by <see cref="ConfigureApplicationPreScopeStart"/>.<br/>
    /// * For header security test check: https://securityheaders.com/ or https://csp-evaluator.withgoogle.com <br/>
    /// * For details check: https://www.nuget.org/packages/NetEscapades.AspNetCore.SecurityHeaders.<br/>
    /// * https://andrewlock.net/major-updates-to-netescapades-aspnetcore-security-headers/ <br/>
    /// * https://andrewlock.net/series/understanding-cross-origin-security-headers <br/>
    /// * For additional security checklist: https://mvsp.dev/
    /// </summary>
    /// <param name="policies">Defines the policies to use for customizing security headers for a request added by NetEscapades.AspNetCore.SecurityHeaders</param>
    /// <param name="serviceProvider"></param>
    /// <param name="appSettings"></param>
    protected virtual void ConfigureDefaultSecurityHeaders(HeaderPolicyCollection policies, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        var policyCollection = policies.RemoveServerHeader()
            .AddFrameOptionsDeny()
            .AddContentTypeOptionsNoSniff()
            .AddReferrerPolicyStrictOriginWhenCrossOrigin()
            .AddContentSecurityPolicy(ConfigureDefaultCsp)
            .AddCrossOriginOpenerPolicy(x => x.SameOrigin())
            .AddCrossOriginEmbedderPolicy(builder => builder.Credentialless())
            .AddCrossOriginResourcePolicy(builder => builder.SameSite())
            .AddPermissionsPolicy(builder =>
            {
                builder.AddDefaultSecureDirectives();
                builder.AddFullscreen().Self();
            });

        if (!appSettings.IsDevEnvironment)
        {
            //https://hstspreload.org/ preload can be risky
            //What to Do When Your Certificate Fails
            //Monitor proactively, Automate renewal, Test staging first,
            //Emergency response plan Know how to deploy a cert fix in < 5 min (e.g., via CI/CD or infra-as-code).
            policyCollection.AddStrictTransportSecurity(63072000, true, false);
        }
    }

    /// <summary>
    /// <ul>
    /// <li>https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP#strict_csp</li>
    /// <li>https://dl.acm.org/doi/pdf/10.1145/2976749.2978363</li>
    /// <li>https://www.netlify.com/blog/general-availability-content-security-policy-csp-nonce-integration/</li>
    /// <li>https://developer.mozilla.org/en-US/docs/Web/HTML/Global_attributes/nonce</li>
    /// <li>https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy/default-src</li>
    /// <li>For header security test check: https://securityheaders.com/ or https://csp-evaluator.withgoogle.com</li>
    /// </ul>
    /// </summary>
    protected virtual void ConfigureDefaultCsp(CspBuilder builder)
    {
        ConfigureDefaultCspBase(builder);
        builder.AddScriptSrc().WithNonce();
    }

    protected virtual void ConfigureDefaultCspBase(CspBuilder builder)
    {
        builder.AddBaseUri().Self();
        builder.AddFormAction().Self();

        builder.AddObjectSrc().None();
        builder.AddFrameAncestors().None();
        builder.AddScriptSrcAttr().None();
    }

    protected virtual void ConfigureSecurityHeaderPolicyBuilder(SecurityHeaderPolicyBuilder builder, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        //todo: csp policy dictionary
        var selfCsp = new HeaderPolicyCollection();
        ConfigureDefaultSecurityHeaders(selfCsp, serviceProvider, appSettings);
        selfCsp.Remove("Content-Security-Policy");
        selfCsp.AddContentSecurityPolicy(x =>
        {
            ConfigureDefaultCspBase(x);
            x.AddScriptSrc().Self();
        });
        builder.AddPolicy(CspFor.CspPolicySelf, selfCsp);

        var inlineCspPolicy = new HeaderPolicyCollection();
        ConfigureDefaultSecurityHeaders(inlineCspPolicy, serviceProvider, appSettings);
        inlineCspPolicy.Remove("Content-Security-Policy");
        inlineCspPolicy.AddContentSecurityPolicy(x =>
        {
            ConfigureDefaultCspBase(x);
            x.AddScriptSrc().Self().UnsafeInline();
        });
        builder.AddPolicy(CspFor.CspPolicyInline, inlineCspPolicy);

        builder.SetPolicySelector(x =>
        {
            var context = x.HttpContext;
            var isSwaggerPath = context.Request.Path.Value?.Contains("swagger", StringComparison.OrdinalIgnoreCase) ?? false;
            if (isSwaggerPath)
                return x.ConfiguredPolicies[CspFor.CspPolicySelf];

            var policyApplied = context.Items.TryGetValue(CspFor.CspPolicyName, out var policy);
            if (!policyApplied)
                return x.DefaultPolicy;

            return (policy as string) switch
            {
                CspFor.CspPolicySelf => x.ConfiguredPolicies[CspFor.CspPolicySelf],
                CspFor.CspPolicyInline => x.ConfiguredPolicies[CspFor.CspPolicyInline],
                _ => x.DefaultPolicy
            };
        });
    }

    private Action<CookiePolicyOptions> GetConfigureCookiePolicy(IAppSettings appSettings)
    {
        return options => ConfigureCookiePolicy(options, appSettings);
    }

    private Action<CookieTempDataProviderOptions> GetConfigureCookieTempDataProvider(IAppSettings appSettings)
    {
        return options => ConfigureCookieTempDataProvider(options, appSettings);
    }

    protected virtual void ConfigureCookiePolicy(CookiePolicyOptions options, IAppSettings appSettings)
    {
        //https://learn.microsoft.com/en-us/aspnet/core/security/gdpr
        options.HttpOnly = HttpOnlyPolicy.None; //Ensures cookies are accessible via JavaScript, use with strict csp
        options.MinimumSameSitePolicy = SameSiteMode.Strict;
        options.Secure = appSettings.IsDevEnvironment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        options.ConsentCookieValue = ConsentCookie.DefaultValue.Encode();
        // default cookie name(.AspNet.Consent) exposes server
        options.ConsentCookie.Name = appSettings.GetAppSpecificName("CookieConsent");
        options.CheckConsentNeeded = _ => true; // user consent for non-essential cookies is needed for a given request.
    }

    protected virtual void ConfigureCookieTempDataProvider(CookieTempDataProviderOptions options, IAppSettings appSettings)
    {
        //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    }

    /// <summary>
    ///  https://github.com/dotnet/aspnetcore/issues/44666
    /// </summary>
    protected virtual Action<SecurityStampValidatorOptions> ConfigureSecurityStampValidatorOptions(IAppSettings appSettings) =>
        options =>
        {
            options.OnRefreshingPrincipal = context =>
            {
                var currentIdentity = (ClaimsIdentity?)context.CurrentPrincipal?.Identity;
                var newIdentity = (ClaimsIdentity?)context.NewPrincipal?.Identity;

                if (currentIdentity == null || newIdentity == null)
                    return Task.CompletedTask;

                var amrClaim = currentIdentity.FindFirst(ClaimConventions.AuthenticationMethodReference);
                if (amrClaim == null) return Task.CompletedTask;

                var existingAmrClaim = newIdentity.FindFirst(ClaimConventions.AuthenticationMethodReference);
                if (existingAmrClaim != null && amrClaim.Value == existingAmrClaim.Value)
                    return Task.CompletedTask;

                newIdentity.AddClaim(new Claim(amrClaim.Type, amrClaim.Value));
                return Task.CompletedTask;
            };
        };

    /// <summary>
    /// Configures static file serving with HTTPS compression enabled.
    /// <para>
    /// <b>Cache-Control: public</b> enables server-side caching of compressed bytes via ResponseCaching middleware.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files">Static files in ASP.NET Core</a></item>
    ///   <item><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression#compression-with-https">Compression with HTTPS</a></item>
    /// </list>
    /// </remarks>
    protected virtual Action<StaticFileOptions> ConfigureStaticFileOptions(IAppSettings appSettings) =>
        options =>
        {
            options.HttpsCompression = HttpsCompressionMode.Compress;
            options.OnPrepareResponse = context =>
            {
                // Note: This 'public' header triggers the ResponseCaching middleware placed before UseStaticFiles in the pipeline.
                // This allows the server to cache the compressed bytes of static assets in memory.
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000"; // 1 year
            };
        };

    protected virtual Action<ForwardedHeadersOptions> ConfigureForwardedHeadersOptions(IAppSettings appSettings)
    {
        return options => { options.ForwardedHeaders = ForwardedHeaders.All; };
    }

    protected virtual Action<RequestLocalizationOptions> ConfigureRequestLocalizationOptions(IAppSettings appSettings)
    {
        var locOptions = appSettings.Localization;
        return options =>
        {
            var cookieRequestCultureProvider = new CookieRequestCultureProvider();
            cookieRequestCultureProvider.CookieName = appSettings.GetAppSpecificName("Culture");

            options.RequestCultureProviders.Clear();
            options.RequestCultureProviders.Add(cookieRequestCultureProvider);
            options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
            options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
            options.SetDefaultCulture(locOptions.DefaultCulture)
                .AddSupportedCultures(locOptions.SupportedCultures)
                .AddSupportedUICultures(locOptions.SupportedCultures);
        };
    }

    protected virtual Action<HostFilteringOptions> ConfigureHostFilteringOptions(IAppSettings appSettings)
    {
        return options =>
        {
            if (options.AllowedHosts.Count != 0) return;

            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts = appSettings.Configuration["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries);
            // Fall back to "*" to disable.
            options.AllowedHosts = hosts?.Length > 0 ? hosts : ["*"];
        };
    }

    /// <summary>
    /// Commonly used for improving behavior not returning a response
    /// </summary>
    /// <param name="application"></param>
    /// <param name="appSettings"></param>
    protected virtual void ConfigureApplicationPipelineStart(WebApplication application, IAppSettings appSettings)
    {
        application.UseForwardedHeaders();
        application.UseHostFiltering();
        application.UseCookiePolicy();
        application.UseSecurityHeaders();
    }

    /// <summary>
    /// Commonly used for a short-circuiting pipeline with a response such as static resources.
    /// </summary>
    protected virtual void ConfigureApplicationPreScopeStart(WebApplication application, IAppSettings appSettings)
    {
        // For Performance, Caching placed before Compression.
        // This ensures the server caches the ALREADY COMPRESSED bytes in memory, saving CPU cycles on every cache hit.
        // By placing these before UseStaticFiles, static resources are also compressed and cached server-side.
        application.UseResponseCaching();
        application.UseResponseCompression();
        application.UseStaticFiles();
    }

    protected virtual void ConfigureApplicationPostScopeStart(WebApplication application, IAppSettings appSettings)
    {
    }

    protected virtual void ConfigureApplicationPreAuthentication(WebApplication application, IAppSettings appSettings)
    {
        if (appSettings.Localization.Enabled)
            application.UseRequestLocalization();
    }

    //todo review stability when no auth is configured
    /// <summary>
    /// Called when PreAuthorization
    /// </summary>
    protected virtual void ConfigureApplicationPostAuthentication(WebApplication application, IAppSettings appSettings)
    {
        var exemptionOptions = application.Services.GetRequiredService<MfaExemptionOptions>();
        var exemptionConfig = ConfigureMFAExemption();
        if (exemptionConfig != null)
        {
            exemptionOptions.MapFromConfig(exemptionConfig);
            application.UseMiddleware<MfaExemptionMiddleware>();
        }

        var redirectionOptions = application.Services.GetRequiredService<MfaRedirectionOptions>();
        var redirectionConfig = ConfigureMFARedirection();
        if (redirectionConfig != null)
        {
            redirectionOptions.MapFromConfig(redirectionConfig);
            application.UseMiddleware<MfaRedirectionMiddleware>();
        }
    }

    protected virtual void ConfigureApplicationPostAuthorization(WebApplication application, IAppSettings appSettings)
    {
        if (!DrnProgramSwaggerOptions.AddSwagger) return;

        application.MapSwagger(DrnProgramSwaggerOptions.DefaultRouteTemplate, DrnProgramSwaggerOptions.ConfigureSwaggerEndpointOptions);
        application.UseSwaggerUI(DrnProgramSwaggerOptions.ConfigureSwaggerUIOptionsAction);
    }

    protected virtual void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        application.MapControllers();
        application.MapRazorPages();
    }

    /// <summary>
    /// Configures MFA (Multi-Factor Authentication) redirection logic when return value is not null:
    /// <ul>
    ///   <li>Redirects to <c>MFALoginUrl</c> if <c>MFAInProgress</c> is true for the user is logged in with single factor</li>
    ///   <li>Redirects to <c>MFASetupUrl</c> if <c>MFASetupRequired</c> is true for a new user without MFA configured.</li>
    ///   <li>Prevents misuse or abuse of <c>MFALoginUrl</c> and <c>MFASetupUrl</c> routes.</li>
    /// </ul>
    /// </summary>
    protected virtual MfaRedirectionConfig? ConfigureMFARedirection() => null;

    protected virtual MfaExemptionConfig? ConfigureMFAExemption() => null;

    /// <summary>
    /// Configures authorization policies and default behaviors for the application.
    /// </summary>
    /// <param name="options">The <see cref="AuthorizationOptions"/> to configure.</param>
    /// <remarks>
    /// With default behavior, this method enforces MFA and performs the following actions:
    /// <ul>
    ///   <li>Adds the <c>MFA</c> policy.</li>
    ///   <li>Adds the <c>MFAExempt</c> policy.</li>
    ///   <li>Sets the default policy to the <c>MFA</c> policy.</li>
    ///   <li>Sets the fallback policy to the <c>MFA</c> policy to enforce MFA on unauthenticated or unhandled requests.</li>
    /// </ul>
    /// </remarks>
    protected virtual void ConfigureAuthorizationOptions(AuthorizationOptions options)
    {
        options.AddPolicy(AuthPolicy.Mfa, policy => policy.AddRequirements(new MfaRequirement()));
        options.AddPolicy(AuthPolicy.MfaExempt, policy => policy.AddRequirements(new MfaExemptRequirement()));

        options.DefaultPolicy = options.GetPolicy(AuthPolicy.Mfa)!;
        options.FallbackPolicy = options.GetPolicy(AuthPolicy.Mfa)!;
    }

    /// <summary>
    /// Sensible defaults for response caching.
    /// <para>
    /// <b>Caching Behavior:</b>
    /// <list type="bullet">
    ///   <item><b>Static Assets:</b> Automatically cached because <see cref="ConfigureStaticFileOptions"/> adds 'public' cache headers.</item>
    ///   <item><b>Dynamic API/Pages:</b> NOT cached by default. Use <c>[ResponseCache]</c> attribute to opt-in.</item>
    ///   <item><b>Auth/Security:</b> Middleware automatically ignores responses with <c>Set-Cookie</c> or <c>Authorization</c> headers for safety.</item>
    /// </list>
    /// </para>
    /// </summary>
    protected virtual void ConfigureResponseCachingOptions(ResponseCachingOptions options)
    {
        options.MaximumBodySize = 16 * 1024 * 1024; // 16 MB safety limit for memory preservation
        options.UseCaseSensitivePaths = false;
    }

    /// <summary>
    /// Configures response compression with security-first defaults.
    /// <para><b>References:</b></para>
    /// <list type="bullet">
    ///   <item><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression">Response compression in ASP.NET Core</a></item>
    ///   <item><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression#compression-with-https">Compression with HTTPS (BREACH/CRIME)</a></item>
    ///   <item><a href="https://learn.microsoft.com/en-us/aspnet/core/performance/caching/middleware">Response Caching Middleware</a></item>
    ///   <item><a href="https://en.wikipedia.org/wiki/BREACH">BREACH attack (Wikipedia)</a></item>
    /// </list>
    /// </summary>
    protected virtual void ConfigureResponseCompressionOptions(ResponseCompressionOptions options)
    {
        // STATIC ASSETS (CSS, JS, fonts, images) ARE SAFELY COMPRESSED via ConfigureStaticFileOptions
        // because they contain no per-user secrets and are identical for all users.
        // References:
        //   - Caching exclusion conditions: https://learn.microsoft.com/en-us/aspnet/core/performance/caching/middleware#conditions-for-caching
        //   - Response compression middleware: https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression
        options.EnableForHttps = false;
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        [
            "text/css",
            "application/javascript",
            "image/svg+xml",
            // Font MIME types: modern + legacy for maximum compatibility
            "font/ttf",
            "application/x-font-ttf",
            "font/otf",
            "font/opentype",
            "font/woff",
            "application/font-woff",
            "font/woff2",
            "application/font-woff2"
        ]);
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    }

    /// <summary>
    /// Configures Brotli and Gzip compression provider options.
    /// Override <see cref="ConfigureBrotliCompressionLevel"/> or <see cref="ConfigureGzipCompressionLevel"/>
    /// to customize compression levels for specific workloads.
    /// </summary>
    protected virtual void ConfigureCompressionProviders(IServiceCollection services)
    {
        // SmallestSize: Maximum compression because only static files are compressed.
        // CPU cost is paid once, then ResponseCaching serves compressed bytes from memory.
        // Bandwidth savings compound across all users.
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = ConfigureBrotliCompressionLevel());
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = ConfigureGzipCompressionLevel());
    }

    /// <summary>
    /// Returns the Brotli compression level. Override to customize.
    /// <para>
    /// Since only static files are compressed (HTTPS dynamic content is excluded for BREACH prevention),
    /// maximum compression is optimal: CPU cost is paid once, compressed bytes are cached by ResponseCaching,
    /// and bandwidth savings compound across all subsequent requests.
    /// </para>
    /// <list type="bullet">
    ///   <item><b>0-3:</b> Fast, low compression (real-time streaming)</item>
    ///   <item><b>4-6:</b> Balanced (dynamic content if HTTPS compression were enabled)</item>
    ///   <item><b>7-11:</b> Maximum compression (static assets—compress once, cache forever)</item>
    /// </list>
    /// Default: <see cref="CompressionLevel.SmallestSize"/> (Level 11 equivalent)
    /// </summary>
    protected virtual CompressionLevel ConfigureBrotliCompressionLevel() => CompressionLevel.SmallestSize;

    /// <summary>
    /// Returns the Gzip compression level. Override to customize.
    /// <para>
    /// Same rationale as Brotli: static files are compressed once and cached,
    /// so maximum compression maximizes bandwidth savings with no per-request CPU cost.
    /// </para>
    /// Default: <see cref="CompressionLevel.SmallestSize"/>
    /// </summary>
    protected virtual CompressionLevel ConfigureGzipCompressionLevel() => CompressionLevel.SmallestSize;

    protected virtual void ConfigureMvcOptions(MvcOptions options)
    {
    }

    protected virtual void ConfigureMvcBuilder(IMvcBuilder mvcBuilder, IAppSettings appSettings)
    {
        var programAssembly = typeof(TProgram).Assembly;
        var partName = typeof(TProgram).GetAssemblyName();
        var applicationParts = mvcBuilder.PartManager.ApplicationParts;
        var controllersAdded = applicationParts.Any(p => p.Name == partName);
        if (!controllersAdded) mvcBuilder.AddApplicationPart(programAssembly);

        mvcBuilder.AddControllersAsServices();
        mvcBuilder.AddJsonOptions(options => JsonConventions.SetHtmlSafeWebJsonDefaults(options.JsonSerializerOptions));

        if (appSettings.IsDevEnvironment)
            mvcBuilder.AddRazorRuntimeCompilation();
    }

    protected virtual void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        options.OpenApiInfo.Title = appSettings.ApplicationName;
        options.AddSwagger = appSettings.IsDevEnvironment;
    }

    protected virtual void ValidateEndpoints(WebApplication application, IAppSettings appSettings)
    {
        if (appSettings.DevelopmentSettings.TemporaryApplication) return;
        // We don't know if user code called UseEndpoints(), so we will call it just in case, UseEndpoints() will ignore duplicate DataSources
        application.UseEndpoints(_ => { });

        var helper = application.Services.GetRequiredService<IEndpointHelper>();
        EndpointCollectionBase<TProgram>.SetEndpointDataSource(helper);
    }

    protected virtual async Task ValidateServicesAsync(WebApplication application, IScopedLog scopeLog) =>
        await application.Services.ValidateServicesAddedByAttributesAsync(scopeLog);

    private static string GetApplicationAssemblyName() => typeof(TProgram).GetAssemblyName();
    private static Assembly GetApplicationAssembly() => typeof(TProgram).Assembly;
}