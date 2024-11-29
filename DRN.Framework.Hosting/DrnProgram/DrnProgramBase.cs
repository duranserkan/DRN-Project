using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Consent;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Common;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
/// <li><a href="https://stackoverflow.com/questions/57846127/what-are-the-differences-between-app-userouting-and-app-useendpoints">UseRouting vs UseEndpoints</a></li>
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
            .CreateBootstrapLogger();
        var logger = bootstrapLogger.ForContext<TProgram>();

        try
        {
            scopedLog.AddToActions("Creating Application");
            var application = await CreateApplicationAsync(args, appSettings, scopedLog);
            scopedLog.Add(nameof(DrnAppFeatures.TemporaryApplication), appSettings.Features.TemporaryApplication);
            scopedLog.Add(nameof(DrnAppFeatures.SkipValidation), appSettings.Features.SkipValidation);
            scopedLog.Add(nameof(DrnAppFeatures.AutoMigrateDevEnvironment), appSettings.Features.AutoMigrateDevEnvironment);
            scopedLog.Add(nameof(AppSettings.Environment), appSettings.Environment);

            scopedLog.AddToActions("Running Application");
            Log.Warning("{@Logs}", scopedLog.Logs);

            if (appSettings.Features.TemporaryApplication)
                return;

            await application.RunAsync();
            scopedLog.AddToActions("Application Shutdown Gracefully");
        }
        catch (Exception exception)
        {
            scopedLog.AddException(exception);
            throw;
        }
        finally
        {
            if (scopedLog.HasException)
                logger.Error("{@Logs}", scopedLog.Logs);
            else
                logger.Warning("{@Logs}", scopedLog.Logs);

            bootstrapLogger.Dispose();
        }
    }

    public static async Task<WebApplication> CreateApplicationAsync(string[]? args, IAppSettings appSettings, IScopedLog scopeLog)
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

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application, appSettings);
        program.ValidateEndpoints(application, appSettings);
        program.ValidateServices(application, scopeLog);

        return application;
    }

    protected abstract Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog);

    protected virtual LoggerConfiguration ConfigureLogger(IConfiguration configuration)
        => new LoggerConfiguration().ReadFrom.Configuration(configuration);

    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder, IAppSettings appSettings)
    {
        applicationBuilder.Host.UseSerilog();
        applicationBuilder.WebHost.UseKestrelCore().ConfigureKestrel(kestrelServerOptions =>
        {
            kestrelServerOptions.AddServerHeader = false;
            kestrelServerOptions.Configure(applicationBuilder.Configuration.GetSection("Kestrel"));
        });

        var services = applicationBuilder.Services;
        services.AdDrnHosting(DrnProgramSwaggerOptions, appSettings.Configuration);

        var mvcBuilder = services.AddMvc(ConfigureMvcOptions);
        ConfigureMvcBuilder(mvcBuilder);

        services.AddAuthorization(ConfigureAuthorizationOptions);
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        services.Configure<CookiePolicyOptions>(GetConfigureCookiePolicy(appSettings));
        services.Configure<ForwardedHeadersOptions>(options => { options.ForwardedHeaders = ForwardedHeaders.All; });
        services.PostConfigure<HostFilteringOptions>(options =>
        {
            if (options.AllowedHosts.Count != 0) return;

            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts = applicationBuilder.Configuration["AllowedHosts"]?.Split(';', StringSplitOptions.RemoveEmptyEntries);
            // Fall back to "*" to disable.
            options.AllowedHosts = hosts?.Length > 0 ? hosts : ["*"];
        });

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

        ConfigureApplicationPreScopeStart(application, appSettings);
        application.UseMiddleware<HttpScopeHandler>();
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
    /// * For details check: https://www.nuget.org/packages/NetEscapades.AspNetCore.SecurityHeaders.<br/>
    /// * For header security test check: https://securityheaders.com/ <br/>
    /// * For additional security checklist: https://mvsp.dev/
    /// </summary>
    /// <param name="policies">Defines the policies to use for customising security headers for a request added by NetEscapades.AspNetCore.SecurityHeaders</param>
    /// <param name="serviceProvider"></param>
    /// <param name="appSettings"></param>
    protected virtual void ConfigureDefaultSecurityHeaders(HeaderPolicyCollection policies, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
        //Todo add nonce tag helper to script for strict csp
        //Todo review default hsts policy, it may affect internal requests
        //https://www.nuget.org/packages/NetEscapades.AspNetCore.SecurityHeaders
        //https://andrewlock.net/major-updates-to-netescapades-aspnetcore-security-headers/
        //https://andrewlock.net/series/understanding-cross-origin-security-headers
        policies.RemoveServerHeader()
            .AddFrameOptionsDeny()
            .AddContentTypeOptionsNoSniff()
            .AddReferrerPolicyStrictOriginWhenCrossOrigin()
            .AddStrictTransportSecurity(63072000, true, true) //https://hstspreload.org/
            .AddContentSecurityPolicy(builder =>
            {
                builder.AddObjectSrc().None();
                builder.AddFormAction().Self();
                builder.AddFrameAncestors().None();
            })
            .AddCrossOriginOpenerPolicy(x => x.SameOrigin())
            .AddCrossOriginEmbedderPolicy(builder => builder.Credentialless())
            .AddCrossOriginResourcePolicy(builder => builder.SameSite())
            .AddPermissionsPolicy(builder =>
            {
                builder.AddDefaultSecureDirectives();
                builder.AddFullscreen().Self();
            });
    }

    protected virtual void ConfigureSecurityHeaderPolicyBuilder(SecurityHeaderPolicyBuilder builder, IServiceProvider serviceProvider, IAppSettings appSettings)
    {
    }

    private Action<CookiePolicyOptions> GetConfigureCookiePolicy(IAppSettings appSettings)
    {
        return options => ConfigureCookiePolicy(options, appSettings);
    }

    protected virtual void ConfigureCookiePolicy(CookiePolicyOptions options, IAppSettings appSettings)
    {
        //https://learn.microsoft.com/en-us/aspnet/core/security/gdpr

        options.HttpOnly = HttpOnlyPolicy.None; //Ensures cookies are accessible via JavaScript, use with strict csp
        options.ConsentCookieValue = Base64Utils.UrlSafeBase64Encode(ConsentCookie.DefaultValue);
        ////default cookie name(.AspNet.Consent) exposes server
        options.ConsentCookie.Name = $".{appSettings.ApplicationName.Replace(' ', '.')}.CookieConsent";
        options.CheckConsentNeeded = context => true; //user consent for non-essential cookies is needed for a given request.
    }

    protected virtual void ConfigureApplicationPreScopeStart(WebApplication application, IAppSettings appSettings)
    {
        application.UseCookiePolicy();
        application.UseSecurityHeaders();

        if (appSettings.Features.UseHttpRequestLogger)
            application.UseMiddleware<HttpRequestLogger>();
    }

    protected virtual void ConfigureApplicationPostScopeStart(WebApplication application, IAppSettings appSettings)
    {
        application.UseHostFiltering();
        application.UseForwardedHeaders();
    }

    protected virtual void ConfigureApplicationPreAuthentication(WebApplication application, IAppSettings appSettings)
    {
    }

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
    ///   <li>Redirects to <c>MFALoginUrl</c> if <c>MFAInProgress</c> is true for the user is logged in with a single factor</li>
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

    protected virtual void ConfigureMvcOptions(MvcOptions options)
    {
    }

    protected virtual void ConfigureMvcBuilder(IMvcBuilder mvcBuilder)
    {
        var programAssembly = typeof(TProgram).Assembly;
        var partName = typeof(TProgram).GetAssemblyName();
        var applicationParts = mvcBuilder.PartManager.ApplicationParts;
        var controllersAdded = applicationParts.Any(p => p.Name == partName);
        if (!controllersAdded) mvcBuilder.AddApplicationPart(programAssembly);

        mvcBuilder.AddControllersAsServices();
        mvcBuilder.AddRazorRuntimeCompilation();
        mvcBuilder.AddJsonOptions(options => JsonConventions.SetJsonDefaults(options.JsonSerializerOptions));
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