# DRN.Framework.Hosting

## Introduction
DRN.Framework.Hosting package provides practical, effective distributed application hosting code with sensible defaults, configuration options.

This package manages configuration, logging, http server (Kestrel) codes and configuration. Since each distributed app at least requires endpoints to support health checking, this packages assumes each distributed application is also a web application.   

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
    public async Task StatusController_Should_Return_Status(TestContext context)
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

        var environment = GetEnvironment(settingJsonName, args, sc);
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
        builder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string?>(nameof(IAppSettings.ApplicationName), applicationName)
        });

        return builder;
    }
    
    //
In the future, DRN.Nexus's remote configuration support will also be added to AddSettingsOverrides.
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

## DrnProgramBase and DrnDefaults

DrnProgramBase has overridable AppBuilderType property which defines WebApplicationBuilder defaults. To check WebApplication and WebApplicationBuilder details see following document,

* learn.microsoft.com/en-us/aspnet/core/migration/50-to-60#new-hosting-model

```csharp
    protected virtual DrnAppBuilderType AppBuilderType => DrnAppBuilderType.DrnDefaults;
```
DrnDefaults are added to empty WebApplicationBuilder and WebApplication and considered as sensible. Further Overriding and fine-tuning options for DrnDefaults can be added in versions after 0.3.0.
```csharp
public enum DrnAppBuilderType
{
    Empty = 1,
    Slim,
    Default,
    DrnDefaults
}
```


---
**Semper Progredi: Always Progressive**