---
name: drn-testing
description: DRN.Framework.Testing - Comprehensive testing infrastructure with DrnTestContext (auto-mocking, DI), ContainerContext (Testcontainers for PostgreSQL), WebApplicationContext (WebApplicationFactory), DataAttributes (DataInline, DataMember), and test providers. Foundation for all DTT (Duran's Testing Technique) test types. Keywords: testing, drntest-context, testcontainers, webapplicationfactory, auto-mocking, nsubstitute, autofixture, data-attributes, integration-testing, unit-testing, dtt
---

# DRN.Framework.Testing

> Comprehensive testing infrastructure enabling DTT (Duran's Testing Technique).

## When to Apply
- Setting up test projects
- Writing unit tests with auto-mocking
- Writing integration tests with testcontainers
- Working with WebApplicationFactory
- Using data attributes and providers

---

## Package Purpose

DRN.Framework.Testing makes testing **easy and encouraging** by providing:
- Auto-mocking via NSubstitute
- Auto-fixture data generation
- PostgreSQL and RabbitMQ testcontainers
- WebApplicationFactory integration
- Convention-based settings/data loading

---

## Directory Structure

```
DRN.Framework.Testing/
├── Contexts/        # DrnTestContext, ContainerContext, WebApplicationContext
├── DataAttributes/  # DataInline, DataMember, DataSelf, DataSelfContext
├── TestAttributes/  # FactDebuggerOnly, TheoryDebuggerOnly
├── Providers/       # SettingsProvider, DataProvider, CredentialsProvider
├── Extensions/      # Test extensions
└── Usings.cs
```

---

## DrnTestContext

Core test context providing service collection and provider.

### Context Augmentations

`DrnTestContext` augments the test environment by:
- **Auto-Registration**: Automatically calls `AddDrnUtils()` (logging, settings, etc.).
- **Startup Jobs**: Triggers `StartupJobRunner` to handle one-time setups (e.g., Auth tokens, global config) defined via `ITestStartupJob`.
- **Method Context**: Captures test method metadata for folder-based settings resolution.

```csharp
[Theory]
[DataInline]
public void MyTest(DrnTestContext context, IMockable mock)
{
    context.ServiceCollection.AddMyServices();
    mock.DoSomething().Returns(42);
    
    var service = context.GetRequiredService<MyService>();
    service.Execute().Should().Be(42);
}
```

### vs DrnTestContextUnit

`DrnTestContextUnit` is a lightweight variant designed for pure unit tests — **no container or application context**. It provides the same DI, auto-mocking, and settings infrastructure but skips heavy integration dependencies.

| Feature | `DrnTestContext` | `DrnTestContextUnit` |
|---------|-------------------|----------------------|
| DI & Auto-mocking | ✅ | ✅ |
| ContainerContext | ✅ | ❌ |
| ApplicationContext | ✅ | ❌ |
| FlurlHttpTest | ✅ | ❌ |
| Data Attributes | `DataInline`, `DataMember`, `DataSelf` | `DataInlineUnit`, `DataMemberUnit`, `DataSelfUnit` |

```csharp
[Theory]
[DataInlineUnit(42)]
public void Fast_Unit_Test(DrnTestContextUnit context, int value, IMyService mock)
{
    mock.GetValue().Returns(value);
    var sut = context.GetRequiredService<MyConsumer>();
    sut.Process().Should().Be(42);
}
```

### Key Properties

| Property | Purpose |
|----------|---------|
| `ServiceCollection` | Add services before building |
| `ContainerContext` | Postgres and RabbitMQ testcontainer management |
| `ApplicationContext` | Full application context (WebApplicationFactory) |
| `FlurlHttpTest` | HTTP mocking for external calls (see below) |
| `Configuration` | Test configuration |

### FlurlHttpTest Integration

Mock external HTTP requests without hitting real endpoints:

```csharp
[Theory]
[DataInline]
public async Task External_API_Should_Be_Mocked(DrnTestContext context)
{
    context.FlurlHttpTest.RespondWith("{ \"status\": \"ok\" }", 200);
    
    context.ServiceCollection.AddSingleton<IApiClient, ApiClient>();
    var client = context.GetRequiredService<IApiClient>();
    
    var result = await client.GetStatusAsync();
    result.Status.Should().Be("ok");
    
    context.FlurlHttpTest.ShouldHaveCalled("https://api.example.com/*").Times(1);
}
```

### Key Methods

| Method | Purpose |
|--------|---------|
| `GetRequiredService<T>()` | Resolve service (builds provider if needed) |
| `BuildServiceProvider()` | Explicitly build provider |
| `ValidateServicesAsync()` | Validate attribute-based services |
| `GetData(path)` | Get test data file content |
| `AddToConfiguration(object)` | Add configuration before service resolution |

---

## ContainerContext

Testcontainer management for integration tests:

```csharp
[Theory]
[DataInline]
public async Task DbTest(DrnTestContext context)
{
    context.ServiceCollection.AddInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    
    var dbContext = context.GetRequiredService<QAContext>();
    dbContext.Users.Add(new User("test"));
    await dbContext.SaveChangesAsync();
}
```

### Features
- Auto-creates PostgreSQL container
- Scans service collection for DrnContexts
- Adds connection strings automatically
- Applies migrations
- Supports RabbitMQ containers

### Isolated Containers

Use `PostgresContextIsolated` when tests need exclusive containers (no sharing):

```csharp
await context.ContainerContext.PostgresIsolated.ApplyMigrationsAsync();
```

### Rapid Prototyping (EnsureDatabaseAsync)

For quick prototyping without manual migrations:

```csharp
await context.ContainerContext.Postgres.EnsureDatabaseAsync();
```

### Advanced Container Configuration

```csharp
var settings = new PostgresContainerSettings
{
    Reuse = true,        // Keep container across test runs
    HostPort = 6432,     // Specific host port
};
context.ContainerContext.Postgres.Configure(settings);
```

### PostgresContainerSettings Defaults

| Property | Default | Notes |
|----------|---------|-------|
| `DefaultPassword` | `"drn"` | Container password |
| `DefaultImage` | `"postgres"` | Docker image |
| `DefaultVersion` | `"18.1-alpine3.23"` | Image tag |
| `Database` | `"drn"` | From `DbContextConventions.DefaultDatabase` |
| `Username` | `"drn"` | From `DbContextConventions.DefaultUsername` |

### Connection String Resolution

| Scenario | Connection Source |
|----------|-------------------|
| `ContainerContext.Postgres` | Auto-generated from Testcontainer |
| `ContainerContext.PostgresIsolated` | Auto-generated from exclusive Testcontainer |
| `ApplicationContext.CreateClientAsync` | Auto-generated from Testcontainer, injected into WebApp |

### DrnDevelopmentSettings Test Flags

Flags automatically managed during testing:

| Flag | Auto-Set Value | Purpose |
|------|----------------|---------|
| `TemporaryApplication` | `true` | Prevents collision with local dev containers |
| `DrnTestContextEnabled` | `true` | Signals test execution environment |

---

## ApplicationContext

WebApplicationFactory wrapper for API testing:

```csharp
[Theory]
[DataInline]
public async Task ApiTest(DrnTestContext context, ITestOutputHelper outputHelper)
{
    // CreateClientAsync starts app, applies migrations, returns authenticated client
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
    
    var endpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
    var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>(endpoint);
    forecasts.Should().NotBeEmpty();
}
```

### Key Methods

| Method | Purpose |
|--------|---------|
| `CreateClientAsync<TProgram>(outputHelper, clientOptions)` | Create authenticated HTTP client |
| `CreateApplicationAndBindDependenciesAsync<TProgram>(outputHelper)` | Bind dependencies without HTTP client |
| `LogToTestOutput(outputHelper, debuggerOnly)` | Direct logs to test output |

### Features
- Syncs DrnTestContext with WebApplicationFactory
- Provides configuration to Program
- Shares service provider
- Automatic PostgreSQL container startup and migrations

---

## Data Attributes

### DataInline

```csharp
[Theory]
[DataInline(99, "test")]
public void Test(DrnTestContext context, int value, string name, Guid autoGenerated)
{
    // value = 99 (inlined)
    // name = "test" (inlined)
    // autoGenerated = auto-fixture Guid
}
```

### DataMember

```csharp
[Theory]
[DataMember(nameof(TestData))]
public void Test(DrnTestContext context, int value, ComplexType obj)
{
    // Data from TestData property
}

public static IEnumerable<object[]> TestData => new List<object[]>
{
    new object[] { 1, new ComplexType() },
    new object[] { 2, new ComplexType() }
};
```

### DataSelf / DataSelfContext

`DataSelfAttribute` provides self-contained test data:

```csharp
public class MyTestData : DataSelfAttribute
{
    public MyTestData()
    {
        AddRow(1, "first");
        AddRow(2, "second");
    }
}

[Theory]
[MyTestData]
public void Test(DrnTestContext context, int id, string name) { }
```

`DataSelfContextAttribute` is the variant for `DrnTestContextUnit`:

```csharp
public class MyUnitTestData : DataSelfContextAttribute
{
    public MyUnitTestData()
    {
        AddRow(1, "first");
    }
}

[Theory]
[MyUnitTestData]
public void UnitTest(DrnTestContextUnit context, int id, string name) { }
```

---

## Test Attributes

| Attribute | Purpose |
|-----------|---------|
| `[FactDebuggerOnly]` | Run only when debugger attached |
| `[TheoryDebuggerOnly]` | Theory only when debugger attached |

Useful for tests requiring real databases or external dependencies.

---

## Providers

### SettingsProvider

```csharp
var appSettings = SettingsProvider.GetAppSettings();
var config = SettingsProvider.GetConfiguration("mySettings");
```

Settings loaded from `Settings/` folder or test file folder.

### DataProvider

```csharp
var content = DataProvider.Get("test-data.json");
```

Data loaded from `Data/` folder or test file folder.

### CredentialsProvider

Generates deterministic, unique test credentials:

```csharp
var credentials = CredentialsProvider.GenerateCredentials();
// credentials.Username, credentials.Password, etc.
```

---

## JSON Utilities

### ValidateObjectSerialization

Contract testing via JSON round-trip validation:

```csharp
var original = new MyDto { Name = "test", Value = 42 };
var roundTripped = original.ValidateObjectSerialization();
roundTripped.Name.Should().Be("test");
roundTripped.Value.Should().Be(42);
```

---

## Auto-Mocking

Interfaces in test parameters are auto-mocked with NSubstitute:

```csharp
[Theory]
[DataInline]
public void Test(DrnTestContext context, IMyService mock)
{
    mock.GetValue().Returns(42);  // NSubstitute mock
    
    context.ServiceCollection.AddScoped<IMyService>(_ => mock);
    // Or context automatically replaces actual service with mock
}
```

---

## DTT Code Snippet

A `dtt` snippet template is provided for rapid test scaffolding. Type `dtt` and expand to get a ready-to-fill test method.

---

## DTT Philosophy: The Pit of Success

DTT is designed to minimize **cognitive cost** and **willpower depletion**. When testing requires high activation energy (manually wiring dependencies, managing container lifecycles, writing boilerplate), developers subconsciously avoid writing tests.

**DTT flips this equation**: By wrapping all complex rigor (containers, migrations, DI, auto-mocking) into a single attribute (`[DataInline]`), it makes the **Right Thing** (high-quality test) accessible via the **Lowest Effort** action.

You no longer need discipline to write great tests; you just follow the easiest path available. With DTT, software testing becomes a natural part of software development.

---

## Global Usings

```csharp
global using Xunit;
global using Xunit.v3;
global using AutoFixture;
global using AutoFixture.AutoNSubstitute;
global using AutoFixture.Xunit3;
global using AwesomeAssertions;
global using NSubstitute;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Configuration;
global using DRN.Framework.Testing;
global using DRN.Framework.Testing.Contexts;
global using DRN.Framework.Testing.DataAttributes;
global using DRN.Framework.Testing.Providers;
global using DRN.Framework.Testing.TestAttributes;
global using DRN.Framework.Utils.Extensions;
global using DRN.Framework.Utils.Settings;
global using DRN.Framework.SharedKernel;
global using DRN.Framework.Utils.DependencyInjection;
global using System.Reflection;
global using System.IO;
global using System.Linq;
global using System.Collections;
global using Xunit.Abstractions;
```

---

## Test Project Setup

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    </PropertyGroup>
    
    <ItemGroup>
        <PackageReference Include="DRN.Framework.Testing" />
        <PackageReference Include="xunit.v3.mtp-v2" />
    </ItemGroup>
    
    <ItemGroup>
        <None Update="Settings\appsettings.json">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </None>
        <None Update="Data\*.txt">
            <CopyToOutputDirectory>Always</CopyToOutputDirectory>
        </None>
    </ItemGroup>
</Project>
```

### xUnit Runner Configuration

```json
// xunit.runner.json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "diagnosticMessages": true,
  "parallelizeTestCollections": true
}
```

---

## Related Skills

- [overview-drn-testing.md](../overview-drn-testing/SKILL.md) - Testing philosophy
- [drn-utils.md](../drn-utils/SKILL.md) - DI and settings
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - Database testing
- [test-unit.md](../test-unit/SKILL.md) - Unit test patterns
- [test-integration.md](../test-integration/SKILL.md) - Integration patterns
- [test-performance.md](../test-performance/SKILL.md) - Performance testing

---
