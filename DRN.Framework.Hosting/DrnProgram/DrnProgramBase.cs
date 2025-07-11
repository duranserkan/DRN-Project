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
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Encodings;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;
using Serilog;

namespace DRN.Framework.Hosting.DrnProgram;

public interface IDrnProgram
{
    static abstract Task Main(string[] args);
}

/// <summary>
/// <li><a href="https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host">Generic host model</a></li>
/// <li><a href="https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60">WebApplication - new hosting model</a></li>
/// <li><a href="https://andrewlock.net/exploring-dotnet-6-part-2-comparing-webapplicationbuilder-to-the-generic-host">Comparing WebApplication to the generic host</a></li>
/// <li><a href="https://andrewlock.net/exploring-dotnet-6-part-3-exploring-the-code-behind-webapplicationbuilder">Code behind WebApplicationBuilder</a></li>
/// <li><a href="https://andrewlock.net/exploring-the-dotnet-8-preview-comparing-createbuilder-to-the-new-createslimbuilder-method">Comparing default builder to slim builder</a></li>
/// <li><a href="https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-part-1">Running async tasks at startup</a></li>
/// <li><a href="https://stackoverflow.com/questions/57846127/what-are-the-differences-between-app-userouting-and-app-useendpoints">UseRouting vs. UseEndpoints</a></li>
/// </summary>
public abstract class DrnProgramBase<TProgram> where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
{
    protected DrnProgramSwaggerOptions DrnProgramSwaggerOptions { get; private set; } = new();
    protected DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;

    protected static async Task RunAsync(string[]? args = null)
    {
        _ = JsonConventions.DefaultOptions;
        var configuration = new ConfigurationBuilder().AddDrnSettings(GetApplicationAssemblyName(), args).Build();
        var appSettings = new AppSettings(configuration);
        var scopedLog = new ScopedLog(appSettings).WithLoggerName(typeof(TProgram).FullName);

        var bootstrapLogger = new TProgram().ConfigureLogger(configuration)
            .Destructure.AsDictionary<SortedDictionary<string, object>>()
            .CreateLogger();
        var logger = bootstrapLogger.ForContext<TProgram>(); //todo replace serilog with nlog 6
        Log.Logger = bootstrapLogger;

        try
        {
            scopedLog.AddToActions("Creating Application");
            var application = await CreateApplicationAsync(args, appSettings, scopedLog);
            scopedLog.Add(nameof(DrnAppFeatures.TemporaryApplication), appSettings.Features.TemporaryApplication);
            scopedLog.Add(nameof(DrnAppFeatures.SkipValidation), appSettings.Features.SkipValidation);
            scopedLog.Add(nameof(DrnAppFeatures.AutoMigrateDevEnvironment), appSettings.Features.AutoMigrateDevEnvironment);
            scopedLog.Add(nameof(DrnAppFeatures.PrototypingMode), appSettings.Features.PrototypingMode);
            scopedLog.Add(nameof(AppSettings.Environment), appSettings.Environment);
            scopedLog.AddToActions("Running Application");
            logger.Warning("{@Logs}", scopedLog.Logs);

            if (appSettings.Features.TemporaryApplication)
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
                logger.Error("{@Logs}", scopedLog.Logs);
            else
                logger.Warning("{@Logs}", scopedLog.Logs);

            await bootstrapLogger.DisposeAsync();
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
                    logger.Error(reportUrl);
                }
            }
        }
        catch (Exception e)
        {
            _ = e; // ignored
        }
    }

    public static async Task<WebApplication> CreateApplicationAsync(string[]? args, IAppSettings appSettings, IScopedLog scopeLog)
    {
        var (program, applicationBuilder) = await CreateApplicationBuilder(args, appSettings, scopeLog);

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application, appSettings);
        var requestPipelineSummary = application.GetRequestPipelineSummary();
        if (appSettings.IsDevEnvironment) //todo send application summaries to nexus for auditing, implement application dependency summary as well
            scopeLog.Add(nameof(RequestPipelineSummary), requestPipelineSummary);

        program.ValidateEndpoints(application, appSettings);
        program.ValidateServices(application, scopeLog);

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

    protected abstract Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings,
        IScopedLog scopedLog);

    protected virtual LoggerConfiguration ConfigureLogger(IConfiguration configuration)
        => new LoggerConfiguration().ReadFrom.Configuration(configuration);

    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        applicationBuilder.Host.UseSerilog((hostingContext, services, loggerConfiguration)
            => ConfigureSerilog(loggerConfiguration, hostingContext, services));

        applicationBuilder.WebHost.UseKestrelCore().ConfigureKestrel(kestrelServerOptions =>
        {
            kestrelServerOptions.AddServerHeader = false;
            kestrelServerOptions.Configure(applicationBuilder.Configuration.GetSection("Kestrel"));
        });

        applicationBuilder.WebHost.UseStaticWebAssets();

        var services = applicationBuilder.Services;
        services.AdDrnHosting(DrnProgramSwaggerOptions, appSettings.Configuration);
        services.AddSingleton<IEndpointAccessor>(sp =>
        {
            var endpointHelper = sp.GetRequiredService<IEndpointHelper>();
            var endpoints = EndpointCollectionBase<TProgram>.Endpoints;
            var pageEndpoints = EndpointCollectionBase<TProgram>.PageEndpoints;
            var apiEndpoints = EndpointCollectionBase<TProgram>.ApiEndpoints;
            return new EndpointAccessor(endpointHelper, endpoints, apiEndpoints, pageEndpoints, typeof(TProgram));
        });

        services.AddResponseCaching(ConfigureResponseCachingOptions);
        var mvcBuilder = services.AddMvc(ConfigureMvcOptions);
        ConfigureMvcBuilder(mvcBuilder, appSettings);

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = $".{appSettings.AppKey}.Antiforgery";
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.HttpOnly = true;
        });

        services.AddAuthorization(ConfigureAuthorizationOptions);
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        services.Configure<CookiePolicyOptions>(GetConfigureCookiePolicy(appSettings));
        services.Configure<SecurityStampValidatorOptions>(ConfigureSecurityStampValidatorOptions(appSettings));
        services.Configure<CookieTempDataProviderOptions>(GetConfigureCookieTempDataProvider(appSettings));
        services.Configure<StaticFileOptions>(ConfigureStaticFileOptions(appSettings));
        services.Configure<ForwardedHeadersOptions>(ConfigureForwardedHeadersOptions(appSettings));
        services.PostConfigure<HostFilteringOptions>(ConfigureHostFilteringOptions(appSettings));

        services.AddSecurityHeaderPolicies((builder, provider) =>
        {
            var policyCollection = new HeaderPolicyCollection();
            ConfigureDefaultSecurityHeaders(policyCollection, provider, appSettings);
            builder.SetDefaultPolicy(policyCollection);
            ConfigureSecurityHeaderPolicyBuilder(builder, provider, appSettings);
        });
    }

    protected virtual void ConfigureSerilog(LoggerConfiguration loggerConfiguration, HostBuilderContext hostingContext, IServiceProvider services)
    {
        loggerConfiguration
            .ReadFrom.Configuration(hostingContext.Configuration)
            .ReadFrom.Services(services) // Allow injecting services (e.g. IHttpContextAccessor) into enrichers
            .Destructure.AsDictionary<SortedDictionary<string, object>>();
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
        application.UseResponseCaching();
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
        //Todo review default hsts policy, it may affect internal requests
        policies.RemoveServerHeader()
            .AddFrameOptionsDeny()
            .AddContentTypeOptionsNoSniff()
            .AddReferrerPolicyStrictOriginWhenCrossOrigin()
            //.AddStrictTransportSecurity(63072000, true, true) //https://hstspreload.org/
            .AddContentSecurityPolicy(ConfigureDefaultCsp)
            .AddCrossOriginOpenerPolicy(x => x.SameOrigin())
            .AddCrossOriginEmbedderPolicy(builder => builder.Credentialless())
            .AddCrossOriginResourcePolicy(builder => builder.SameSite())
            .AddPermissionsPolicy(builder =>
            {
                builder.AddDefaultSecureDirectives();
                builder.AddFullscreen().Self();
            });
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
        options.Secure = CookieSecurePolicy.SameAsRequest;

        options.ConsentCookieValue = Base64Utils.UrlSafeBase64Encode(ConsentCookie.DefaultValue);
        //default cookie name(.AspNet.Consent) exposes server
        options.ConsentCookie.Name = $".{appSettings.AppKey}.CookieConsent";
        options.CheckConsentNeeded = _ => true; //user consent for non-essential cookies is needed for a given request.
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

    protected virtual Action<StaticFileOptions> ConfigureStaticFileOptions(IAppSettings appSettings) =>
        options =>
        {
            options.HttpsCompression = HttpsCompressionMode.Compress;
            options.OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000"; // 1 year
            };
        };

    protected virtual Action<ForwardedHeadersOptions> ConfigureForwardedHeadersOptions(IAppSettings appSettings)
    {
        return options => { options.ForwardedHeaders = ForwardedHeaders.All; };
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
        if (appSettings.Features.UseHttpRequestLogger)
            application.UseMiddleware<HttpRequestLogger>(); //todo implement micro logging support

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
        //todo add response caching for static resources and html pages
        //todo consider response compression for static resources or general usage
        application.UseStaticFiles();
    }

    protected virtual void ConfigureApplicationPostScopeStart(WebApplication application, IAppSettings appSettings)
    {
    }

    protected virtual void ConfigureApplicationPreAuthentication(WebApplication application, IAppSettings appSettings)
    {
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

    /// <summary>
    /// Configures MFA (Multi-Factor Authentication) redirection logic when return value is not null:
    /// <ul>
    ///   <li>Redirects to <c>MFALoginUrl</c> if <c>MFAInProgress</c> is true for the user is logged in with a single factor</li>
    ///   <li>Redirects to <c>MFASetupUrl</c> if <c>MFASetupRequired</c> is true for a new user without MFA configured.</li>
    ///   <li>Prevents misuse or abuse of <c>MFALoginUrl</c> and <c>MFASetupUrl</c> routes.</li>
    /// </ul>
    /// </summary>
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

    protected virtual void ConfigureResponseCachingOptions(ResponseCachingOptions options)
    {
    }

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
        if (appSettings.Features.TemporaryApplication) return;
        // We don't know if user code called UseEndpoints(), so we will call it just in case, UseEndpoints() will ignore duplicate DataSources
        application.UseEndpoints(_ => { });

        var helper = application.Services.GetRequiredService<IEndpointHelper>();
        EndpointCollectionBase<TProgram>.SetEndpointDataSource(helper);
    }

    protected virtual void ValidateServices(WebApplication application, IScopedLog scopeLog) =>
        application.Services.ValidateServicesAddedByAttributes(scopeLog);

    private static string GetApplicationAssemblyName() => typeof(TProgram).GetAssemblyName();
}