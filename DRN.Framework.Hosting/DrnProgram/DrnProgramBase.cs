using DRN.Framework.Hosting.Extensions;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DRN.Framework.Hosting.DrnProgram;

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

    protected DrnProgramOptions DrnProgramOptions { get; } = new();

    protected static async Task RunAsync(string[]? args = null)
    {
        _ = JsonConventions.DefaultOptions;
        Configuration = new ConfigurationBuilder().AddDrnSettings(GetApplicationName(), args).Build();
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
            ApplicationName = GetApplicationName(),
            EnvironmentName = AppSettings.Environment.ToString()
        };

        var applicationBuilder = DrnProgramConventions.GetApplicationBuilder<TProgram>(options, program.DrnProgramOptions.AppBuilderType);
        applicationBuilder.Configuration.AddDrnSettings(GetApplicationName(), args);
        program.ConfigureApplicationBuilder(applicationBuilder);
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
        applicationBuilder.Services.ConfigureHttpJsonOptions(options => JsonConventions.SetJsonDefaults(options.SerializerOptions));
        applicationBuilder.Services.AddLogging();
        applicationBuilder.Services.AddEndpointsApiExplorer();
        applicationBuilder.Services.AdDrnHosting();

        if (DrnProgramOptions.AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        var mvcBuilder = applicationBuilder.Services.AddMvc(ConfigureMvcOptions)
            .AddJsonOptions(options => JsonConventions.SetJsonDefaults(options.JsonSerializerOptions));
        var programAssembly = typeof(TProgram).Assembly;
        var partName = typeof(TProgram).GetAssemblyName();
        var applicationParts = mvcBuilder.PartManager.ApplicationParts;
        var controllersAdded = applicationParts.Any(p => p.Name == partName);
        if (!controllersAdded) mvcBuilder.AddApplicationPart(programAssembly);

        applicationBuilder.Services.Configure<ForwardedHeadersOptions>(options => { options.ForwardedHeaders = ForwardedHeaders.All; });
        applicationBuilder.Services.PostConfigure<HostFilteringOptions>(options =>
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
        application.Services.ValidateServicesAddedByAttributes();
        if (DrnProgramOptions.AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        ConfigureApplicationPreScopeStart(application);
        application.UseMiddleware<HttpScopeHandler>();
        ConfigureApplicationPostScopeStart(application);

        if (DrnProgramOptions.UseHttpRequestLogger)
            application.UseMiddleware<HttpRequestLogger>();

        application.UseHostFiltering();
        application.UseForwardedHeaders();
        application.UseRouting();

        ConfigureApplicationPreAuth(application);
        application.UseAuthentication();
        application.UseAuthorization();
        ConfigureApplicationPostAuth(application);

        MapApplicationEndpoints(application);
    }

    protected virtual void ConfigureApplicationPreScopeStart(WebApplication application)
    {
    }

    protected virtual void ConfigureApplicationPostScopeStart(WebApplication application)
    {
    }

    protected virtual void ConfigureApplicationPreAuth(WebApplication application)
    {
    }

    protected virtual void ConfigureApplicationPostAuth(WebApplication application)
    {
    }

    protected virtual void MapApplicationEndpoints(WebApplication application)
    {
        application.MapControllers();
    }

    protected virtual void ConfigureMvcOptions(MvcOptions options)
    {
    }

    private static string GetApplicationName() => typeof(TProgram).GetAssemblyName();
}