using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private static IConfiguration Configuration = new ConfigurationManager();

    protected static ILogger Logger { get; private set; } = Log.Logger;
    protected static IAppSettings AppSettings { get; private set; } = new AppSettings(new ConfigurationManager());
    protected static IScopedLog ScopedLog { get; } = new ScopedLog().WithLoggerName(typeof(TProgram).FullName);
    protected static DrnProgramSwaggerOptions DrnProgramSwaggerOptions { get; private set; } = new();
    protected DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;

    protected static async Task RunAsync(string[]? args = null)
    {
        _ = JsonConventions.DefaultOptions;
        Configuration = new ConfigurationBuilder().AddDrnSettings(GetApplicationAssemblyName(), args).Build();
        AppSettings = new AppSettings(Configuration, true);
        Logger = new TProgram().ConfigureLogger()
            .Destructure.AsDictionary<SortedDictionary<string, object>>()
            .CreateBootstrapLogger().ForContext<TProgram>();
        Log.Logger = Logger;

        try
        {
            ScopedLog.AddToActions("Creating Application");
            var application = await CreateApplicationAsync(args);

            ScopedLog.AddToActions("Running Application");
            Log.Warning("{@Logs}", ScopedLog.Logs);

            await application.RunAsync();

            ScopedLog.AddToActions("Application Shutdown Gracefully");
        }
        catch (Exception exception)
        {
            ScopedLog.AddException(exception);
        }
        finally
        {
            if (ScopedLog.HasException)
                Log.Error("{@Logs}", ScopedLog.Logs);
            else
                Log.Warning("{@Logs}", ScopedLog.Logs);

            await Log.CloseAndFlushAsync();
        }
    }

    public static async Task<WebApplication> CreateApplicationAsync(string[]? args)
    {
        var program = new TProgram();
        var options = new WebApplicationOptions
        {
            Args = args,
            ApplicationName = GetApplicationAssemblyName(),
            EnvironmentName = AppSettings.Environment.ToString()
        };
        program.ConfigureSwaggerOptions(DrnProgramSwaggerOptions);

        var applicationBuilder = DrnProgramConventions.GetApplicationBuilder<TProgram>(options, program.AppBuilderType);
        applicationBuilder.Configuration.AddDrnSettings(GetApplicationAssemblyName(), args);
        program.ConfigureApplicationBuilder(applicationBuilder);

        applicationBuilder.Services.AddAuthorization(program.ConfigureAuthorizationOptions);
        await program.AddServicesAsync(applicationBuilder);

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application);

        return application;
    }

    protected abstract Task AddServicesAsync(WebApplicationBuilder builder);

    protected virtual LoggerConfiguration ConfigureLogger()
        => new LoggerConfiguration().ReadFrom.Configuration(Configuration);

    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder)
    {
        applicationBuilder.Host.UseSerilog();
        applicationBuilder.WebHost.UseKestrelCore().ConfigureKestrel(kestrelServerOptions =>
            kestrelServerOptions.Configure(applicationBuilder.Configuration.GetSection("Kestrel")));

        var services = applicationBuilder.Services;
        services.AdDrnHosting(DrnProgramSwaggerOptions, Configuration);

        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        var mvcBuilder = services.AddMvc(ConfigureMvcOptions);
        ConfigureMvcBuilder(mvcBuilder);

        services.Configure<ForwardedHeadersOptions>(options => { options.ForwardedHeaders = ForwardedHeaders.All; });
        services.PostConfigure<HostFilteringOptions>(options =>
        {
            if (options.AllowedHosts != null && options.AllowedHosts.Count != 0) return;
            var separator = new[] { ';' };
            // "AllowedHosts": "localhost;127.0.0.1;[::1]"
            var hosts = applicationBuilder.Configuration["AllowedHosts"]?.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            // Fall back to "*" to disable.
            options.AllowedHosts = hosts?.Length > 0 ? hosts : ["*"];
        });
    }

    protected virtual void ConfigureApplication(WebApplication application)
    {
        application.Services.ValidateServicesAddedByAttributes(ScopedLog);
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        ConfigureApplicationPreScopeStart(application);
        application.UseMiddleware<HttpScopeHandler>();
        ConfigureApplicationPostScopeStart(application);

        application.UseRouting();

        ConfigureApplicationPreAuthentication(application);
        application.UseAuthentication();
        application.UseMiddleware<ScopedUserMiddleware>();
        ConfigureApplicationPostAuthentication(application);
        application.UseAuthorization();
        ConfigureApplicationPostAuthorization(application);

        MapApplicationEndpoints(application);
    }

    protected virtual void ConfigureApplicationPreScopeStart(WebApplication application)
    {
        if (AppSettings.Features.UseHttpRequestLogger)
            application.UseMiddleware<HttpRequestLogger>();
    }

    protected virtual void ConfigureApplicationPostScopeStart(WebApplication application)
    {
        application.UseHostFiltering();
        application.UseForwardedHeaders();
    }

    protected virtual void ConfigureApplicationPreAuthentication(WebApplication application)
    {
    }

    /// <summary>
    /// Called when PreAuthorization
    /// </summary>
    protected virtual void ConfigureApplicationPostAuthentication(WebApplication application)
    {
        var config = ConfigureMFARedirection();
        if (config == null) return;

        var options = application.Services.GetRequiredService<MFARedirectionOptions>();
        options.MFALoginUrl = config.MFALoginUrl;
        options.MFASetupUrl = config.MFASetupUrl;
        options.LoginUrl = config.LoginUrl;
        options.LogoutUrl = config.LogoutUrl;
        options.AppPages = config.AppPages;

        application.UseMiddleware<MFARedirectionMiddleware>();
    }

    protected virtual void ConfigureApplicationPostAuthorization(WebApplication application)
    {
        if (!DrnProgramSwaggerOptions.AddSwagger) return;

        application.MapSwagger(DrnProgramSwaggerOptions.DefaultRouteTemplate, DrnProgramSwaggerOptions.ConfigureSwaggerEndpointOptions);
        application.UseSwaggerUI(DrnProgramSwaggerOptions.ConfigureSwaggerUIOptionsAction);
    }

    protected virtual void MapApplicationEndpoints(WebApplication application)
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
    protected virtual MFARedirectionConfig? ConfigureMFARedirection() => null;

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
        options.AddPolicy(AuthPolicy.MFA, policy => policy.AddRequirements(new MFARequirement()));
        options.AddPolicy(AuthPolicy.MFAExempt, policy => policy.AddRequirements(new MFAExemptRequirement()));

        options.DefaultPolicy = options.GetPolicy(AuthPolicy.MFA)!;
        options.FallbackPolicy = options.GetPolicy(AuthPolicy.MFA)!;
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

        mvcBuilder.AddRazorRuntimeCompilation();
        mvcBuilder.AddJsonOptions(options => JsonConventions.SetJsonDefaults(options.JsonSerializerOptions));
    }

    protected virtual void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options)
    {
        options.OpenApiInfo.Title = AppSettings.ApplicationName;
        options.AddSwagger = AppSettings.IsDevEnvironment;
    }

    private static string GetApplicationAssemblyName() => typeof(TProgram).GetAssemblyName();
}