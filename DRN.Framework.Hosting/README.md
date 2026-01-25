# DRN.Framework.Hosting

## Introduction
DRN.Framework.Hosting package provides practical, effective distributed application hosting code with sensible defaults, configuration options.

This package manages configuration, logging, http server (Kestrel) codes and configuration. Since each distributed app at least requires an endpoint to support health checking, this packages assumes each distributed application is also a web application.   

### QuickStart: Basics

Here's a basic test demonstration to take your attention and get you started:
```csharp
using DRN.Framework.Hosting.DrnProgram;
using Sample.Application;
using Sample.Infra;

namespace Sample.Hosted;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override void AddServices(IServiceCollection services) => services
        .AddSampleInfraServices()
        .AddSampleApplicationServices();
}
```
You can easily test your application with DRN.Framework.Testing package.
```csharp
public class StatusControllerTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task StatusController_Should_Return_Status(DrnTestContext context)
    {
        context.ApplicationContext.LogToTestOutput(outputHelper);
        var application = context.ApplicationContext.CreateApplication<Program>();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();

        var client = application.CreateClient();
        var status = await client.GetFromJsonAsync<ConfigurationDebugViewSummary>("Status");
        var programName = typeof(Program).GetAssemblyName();
        status?.ApplicationName.Should().Be(programName);
    }
}
```

## Configuration
DRN hosting package applies configuration in following order:
```csharp
public static IConfigurationBuilder AddDrnSettings(this IConfigurationBuilder builder, string applicationName, string[]? args = null,
    string settingJsonName = "appsettings",
    IServiceCollection? sc = null)
{
    if (string.IsNullOrWhiteSpace(settingJsonName))
        settingJsonName = "appsettings";
    var fileProvider = builder.Properties
        .Where(pair => pair.Value.GetType() == typeof(PhysicalFileProvider))
        .Select(pair => (PhysicalFileProvider)pair.Value).FirstOrDefault();

    var environment = GetEnvironment(settingJsonName, args, sc, fileProvider?.Root);
    builder.AddJsonFile($"{settingJsonName}.json", true);
    builder.AddJsonFile($"{settingJsonName}.{environment.ToString()}.json", true);

    if (applicationName.Length > 0)
        try
        {
            var assembly = Assembly.Load(new AssemblyName(applicationName));
            builder.AddUserSecrets(assembly, true);
        }
        catch (FileNotFoundException e)
        {
            _ = e;
        }

    builder.AddSettingsOverrides(args, sc);

    return builder;
}

private static void AddSettingsOverrides(this IConfigurationBuilder builder, string[]? args, IServiceCollection? sc)
{
    builder.AddEnvironmentVariables("ASPNETCORE_");
    builder.AddEnvironmentVariables("DOTNET_");
    builder.AddEnvironmentVariables();
    builder.AddMountDirectorySettings(sc);

    if (args != null && args.Length > 0)
        builder.AddCommandLine(args);
}
    
/// <summary>
/// Mounted settings like kubernetes secrets or configmaps
/// </summary>
public static IConfigurationBuilder AddMountDirectorySettings(this IConfigurationBuilder builder, IServiceCollection? sc = null)
{
    var overrideService = sc?.BuildServiceProvider().GetService<IMountedSettingsConventionsOverride>();
    var mountOverride = overrideService?.MountedSettingsDirectory;
    if (overrideService != null)
        builder.AddObjectToJsonConfiguration(overrideService);

    builder.AddKeyPerFile(MountedSettingsConventions.KeyPerFileSettingsMountDirectory(mountOverride), true);
    var jsonDirectory = MountedSettingsConventions.JsonSettingDirectoryInfo(mountOverride);
    if (!jsonDirectory.Exists) return builder;

    foreach (var files in jsonDirectory.GetFiles())
        builder.AddJsonFile(files.FullName);

    return builder;
}
```
You can easily obtain effective configuration with appSettings. Api controller is used for demonstration. **Do not expose your configuration**.
```csharp
[ApiController]
[Route("[controller]")]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult Status()
    {
        return Ok(appSettings.GetDebugView().ToSummary());
    }
}
```
## Logging
DrnProgramBase applies Serilog configurations. Console and Graylog sinks are supported by default. To configure logging you can add serilog configs in appsettings.json

```json
{
  "Serilog": {
    "Docs": "https://github.com/serilog/serilog-settings-configuration",
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.Graylog"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[BEGIN {Timestamp:HH:mm:ss.fffffff} {Level:u3} {SourceContext}]{NewLine}{Message:lj}{NewLine}[END {Timestamp:HH:mm:ss.fffffff} {Level:u3} {SourceContext}]{NewLine}"
        }
      },
      {
        "Name": "Graylog",
        "Args": {
          "hostnameOrAddress": "localhost",
          "port": "12201",
          "transportType": "Udp"
        }
      }
    ]
  }
}
```
## Kestrel

DrnProgramBase applies Kestrel configurations. To configure logging you should add kestrel configs in appsettings.json

```json
{
  "Kestrel": {
    "Docs": "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints",
    "EndpointDefaults": {
      "Protocols": "Http1"
    },
    "Endpoints": {
      "All": {
        "Url": "http://*:5988"
      }
    }
  }
}
```

## DrnProgramBase RunAsync
DrnProgramBase handles most of the application level wiring and standardizes JsonDefaults across all of the `System.Text.Json` usages.
```csharp
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
```

## Local Development Infrastructure

You can leverage `DRN.Framework.Testing`'s container management features directly in your hosted application to automatically provision infrastructure (like Postgres) during local development.

### Setup

1.  **Add Conditional Reference**: Add a reference to `DRN.Framework.Testing` that is only active in `Debug` configuration.

    ```xml
    <ItemGroup Condition="'$(Configuration)' == 'Debug'">
        <ProjectReference Include="..\DRN.Framework.Testing\DRN.Framework.Testing.csproj" />
    </ItemGroup>
    ```

2.  **Configure Startup Actions**: Implement `DrnProgramActions` to hook into the application startup and launch dependencies.

    ```csharp
    // Sample.Hosted/SampleProgramActions.cs
    #if DEBUG
    using DRN.Framework.Hosting.DrnProgram;
    using DRN.Framework.Testing.Extensions;
    // ... other usings

    public class SampleProgramActions : DrnProgramActions
    {
        public override async Task ApplicationBuilderCreatedAsync<TProgram>(
            TProgram program, WebApplicationBuilder builder,
            IAppSettings appSettings, IScopedLog scopedLog)
        {
            await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings);
        }
    }
    #endif
    ```

This allows your application to strictly depend on `Hosting` in production while benefiting from `Testing`'s infrastructure tools during development.

## DrnDefaults

DrnProgramBase has a DrnProgramOptions property which defines behavior and defaults to WebApplication and WebApplicationBuilder. See following document for new hosting model introduced with .NET 6,

* https://learn.microsoft.com/en-us/aspnet/core/migration/50-to-60#new-hosting-model

DrnDefaults are added to empty WebApplicationBuilder and WebApplication and considered as sensible and configurable. Further Overriding and fine-tuning options for DrnDefaults can be added in versions after 0.3.0.
```csharp
    protected DrnProgramSwaggerOptions DrnProgramSwaggerOptions { get; private set; } = new();
    
    // ReSharper disable once StaticMemberInGenericType
    protected static NLogAspNetCoreOptions NLogOptions { get; set; } = new()
    {
        ReplaceLoggerFactory = false,
        RemoveLoggerFactoryFilter = false
    };

    protected DrnAppBuilderType AppBuilderType { get; set; } = DrnAppBuilderType.DrnDefaults;

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
        
        // ... (Endpoints & Mvc configuration)

        services.AddAuthorization(ConfigureAuthorizationOptions);
        if (AppBuilderType != DrnAppBuilderType.DrnDefaults) return;

        // ... (Defaults configuration)
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
```

---
**Semper Progressivus: Always Progressive**