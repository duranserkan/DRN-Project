using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;

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
    protected virtual DrnAppBuilderType AppBuilderType => DrnAppBuilderType.DrnDefaults;

    protected static async Task RunAsync(string[]? args = null)
    {
        Log.Logger = new TProgram().ConfigureLogger(args).CreateBootstrapLogger();
        var scopedLog = new ScopedLog().WithLoggerName(typeof(TProgram).FullName);
        try
        {
            scopedLog.AddToActions("Creating Application");
            var application = CreateApplication(args);

            scopedLog.AddToActions("Running Application");
            Log.Information("{@Logs}", scopedLog.Logs);

            await application.RunAsync();

            scopedLog.AddToActions("Application Shutdown Gracefully");
        }
        catch (Exception exception)
        {
            scopedLog.AddException(exception);
        }
        finally
        {
            if (scopedLog.HasException)
                Log.Error("{@Logs}", scopedLog.Logs);
            else
                Log.Information("{@Logs}", scopedLog.Logs);

            await Log.CloseAndFlushAsync();
        }
    }

    public static WebApplication CreateApplication(string[]? args)
    {
        _ = JsonConventions.DefaultOptions;
        var program = new TProgram();
        var options = new WebApplicationOptions
        {
            Args = args,
            ApplicationName = GetApplicationName()
        };

        var applicationBuilder = DrnProgramConventions.GetApplicationBuilder<TProgram>(options, program.AppBuilderType);
        applicationBuilder.Configuration.AddDrnSettings(options.ApplicationName, args);
        program.ConfigureApplicationBuilder(applicationBuilder);
        program.AddServices(applicationBuilder.Services);

        var application = applicationBuilder.Build();
        program.ConfigureApplication(application);

        return application;
    }


    protected virtual void ConfigureApplicationBuilder(WebApplicationBuilder applicationBuilder)
    {
        if (AppBuilderType == DrnAppBuilderType.DrnDefaults)
            DrnProgramConventions.ConfigureDrnApplicationBuilder<TProgram>(applicationBuilder);
    }

    protected virtual void ConfigureApplication(WebApplication application)
    {
        if (AppBuilderType == DrnAppBuilderType.DrnDefaults)
            DrnProgramConventions.ConfigureDrnApplication(application);
    }

    protected virtual LoggerConfiguration ConfigureLogger(string[]? args)
    {
        var loggerConfiguration = new LoggerConfiguration().WriteTo.Console();
        var configuration = new ConfigurationBuilder().AddDrnSettings(GetApplicationName(), args).Build();
        var appSettings = new AppSettings(configuration);
        var graylogHost = appSettings.GetValue("GraylogHostnameOrAddress", string.Empty);

        if (string.IsNullOrWhiteSpace(graylogHost)) return loggerConfiguration;

        var graylogOptions = new GraylogSinkOptions
        {
            MinimumLogEventLevel = LogEventLevel.Information,
            HostnameOrAddress = graylogHost,
            TransportType = TransportType.Udp
        };

        return loggerConfiguration.WriteTo.Graylog(graylogOptions);
    }

    protected abstract void AddServices(IServiceCollection services);

    private static string GetApplicationName() => typeof(TProgram).GetAssemblyName();
}