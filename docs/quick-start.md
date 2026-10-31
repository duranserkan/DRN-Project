# Quick Start

This guide covers prerequisites, building and testing the solution, package installation, and application setup for [DRN-Project](../README.md).

See the [Sample application guide](../Sample.Hosted/README.md) for local startup, Swagger and test-page access, error handling, notification history, and frontend builds and tests.

## Contents

- [Prerequisites](#prerequisites)
- [Build, Run & Test](#build-run--test)
- [Installation](#installation)
- [Create an Application](#create-an-application)
- [Testing](#testing)
- [Developer Environment](#developer-environment)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) 10.0.403 or a later compatible SDK selected by [global.json](../global.json)
- [Docker](https://www.docker.com/) with the daemon running (for the sample app and integration tests with Testcontainers)
- Optional Sample frontend tooling is covered in the [frontend build guide](../Sample.Hosted/README.md#frontend-assets).

> **Nice to have**: [JetBrains Rider](https://www.jetbrains.com/rider/) (recommended), [Visual Studio](https://visualstudio.microsoft.com/), or [VS Code](https://code.visualstudio.com/) with [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit). All operations can also be performed from the terminal.

## Build, Run & Test

Run these commands from the repository root:

```bash
dotnet build DRN.slnx                                  # Build the entire solution
dotnet run --project Sample.Hosted -c Debug              # Run the sample with local dependencies (requires Docker)
dotnet run --project DRN.Test.Unit                      # Run unit tests
dotnet run --project DRN.Test.Analyzer                  # Run analyzer tests
dotnet run --project DRN.Test.Integration               # Run integration tests (requires Docker)
dotnet run --project DRN.Test.Performance -c Release     # Run all performance benchmarks (requires Release build)
dotnet run --project DRN.Test.Performance -c Release -- --filter-method DRN.Test.Performance.Benchmark.Framework.Utils.SourceKnownIdUtilsPerformanceTests.Run_Benchmarks  # Run SKID/SKEID performance benchmark only
dotnet run --project DRN.Test.Performance -c Release -- --filter-method DRN.Test.Performance.Benchmark.Framework.Utils.SourceKnownIdUtilsSaturationPerformanceTests.Run_Benchmarks  # Run SKID/SKEID saturation benchmark only
```

See the [Docker and Docker Compose guide](../Docker/README.md) for local container setup, credentials, data migration, and published image verification.

> The .NET test projects (unit, analyzer, integration, and performance) use [Microsoft Testing Platform (MTP) 2.x](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro) with xUnit v3 and are built as standalone executables. Use `dotnet run --project` to execute them.

> **Debugging tip:** If the application fails to start in `Development` mode, `DrnProgramBase` attempts to generate a `StartupExceptionReport.html` in the output directory with a detailed, browsable error report.

## Installation

For web applications (see [Sample.Hosted.csproj](../Sample.Hosted/Sample.Hosted.csproj)):

```bash
dotnet add package DRN.Framework.Hosting
```

For test projects (see [DRN.Test.Integration.csproj](../DRN.Test.Integration/DRN.Test.Integration.csproj), [DRN.Test.Unit.csproj](../DRN.Test.Unit/DRN.Test.Unit.csproj)):

```bash
dotnet add package DRN.Framework.Testing
```

To create a new project from scratch:

```bash
dotnet new web -n MyApp && cd MyApp && dotnet add package DRN.Framework.Hosting
cd .. && dotnet new xunit -n MyApp.Tests && cd MyApp.Tests && dotnet add package DRN.Framework.Testing
```

After scaffolding, follow the [Testing NuGet README](https://nuget.org/packages/DRN.Framework.Testing/#readme-body-tab) to configure the test runner, using the reference projects linked above.

Use [appsettings.json](../Sample.Hosted/appsettings.json) and [appsettings.Development.json](../Sample.Hosted/appsettings.Development.json) as configuration references. Set `Environment`, `NexusAppSettings:AppId`, and `NexusAppSettings:AppInstanceId` explicitly. For local dependency launch, use `Environment: Development` and `DrnDevelopmentSettings:LaunchExternalDependencies: true` with the startup action below. See the [Hosting NuGet README](https://nuget.org/packages/DRN.Framework.Hosting/#readme-body-tab) for configuration details.

For performance benchmarks, see [DRN.Test.Performance](../DRN.Test.Performance) and its [benchmark reports](../DRN.Test.Performance/Reports).

## Create an Application

Replace the generated `MyApp/Program.cs` with the following minimal example. See [SampleProgram.cs](../Sample.Hosted/SampleProgram.cs) for a full implementation.

```csharp
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

public class Program : DrnProgramBase<Program>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services.AddServicesWithAttributes();
        return Task.CompletedTask;
    }
}
```

To launch local PostgreSQL during development, add a `DrnProgramActions` class that calls `builder.LaunchExternalDependenciesAsync` from `ApplicationBuilderCreatedAsync`, as shown in [SampleProgramActions.cs](../Sample.Hosted/SampleProgramActions.cs). The class is discovered automatically. Wrap the file in `#if DEBUG` and reference `DRN.Framework.Testing` only in Debug builds, following [Sample.Hosted.csproj](../Sample.Hosted/Sample.Hosted.csproj). Launching also requires the Development environment and `LaunchExternalDependencies` setting shown above.

Read the [Hosting](https://nuget.org/packages/DRN.Framework.Hosting/#readme-body-tab) and [Testing](https://nuget.org/packages/DRN.Framework.Testing/#readme-body-tab) NuGet READMEs for configuration details and more examples.

## Testing

Read the [Testing NuGet README](https://nuget.org/packages/DRN.Framework.Testing/#readme-body-tab) for unit and integration testing guidance, and see [QAContextTests.cs](../DRN.Test.Integration/Tests/Sample/Infra/QA/QAContextTests.cs) for a database integration example.

## Developer Environment

Add the following to your shell profile (e.g. `~/.zshrc`) to opt out of telemetry:

```bash
# Opt out of .NET CLI telemetry
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Opt out of .NET Testing Platform telemetry
export TESTINGPLATFORM_TELEMETRY_OPTOUT=1
```
