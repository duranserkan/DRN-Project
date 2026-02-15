---
name: drn-testing
description: DRN.Framework.Testing - Comprehensive testing infrastructure with DrnTestContext (auto-mocking, DI), ContainerContext (Testcontainers for PostgreSQL), WebApplicationContext (WebApplicationFactory), DataAttributes (DataInline, DataMember), and test providers. Foundation for all DTT (Duran's Testing Technique) test types. Keywords: testing, drntest-context, testcontainers, webapplicationfactory, auto-mocking, nsubstitute, autofixture, data-attributes, integration-testing, unit-testing, dtt
last-updated: 2026-02-15
difficulty: intermediate
---

# DRN.Framework.Testing

> DTT (Duran's Testing Technique) testing infrastructure: auto-mocking, Testcontainers, WebApplicationFactory.

## When to Apply
- Setting up test projects
- Writing unit tests with auto-mocking
- Writing integration tests with Testcontainers
- Working with WebApplicationFactory
- Using data attributes and providers

---

## DrnTestContext

Core test context providing DI, auto-mocking, and container management.

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

### DrnTestContext vs DrnTestContextUnit

| Feature | `DrnTestContext` | `DrnTestContextUnit` |
|---------|-------------------|----------------------|
| DI & Auto-mocking | ✅ | ✅ |
| ContainerContext | ✅ | ❌ |
| ApplicationContext | ✅ | ❌ |
| FlurlHttpTest | ✅ | ❌ |
| Data Attributes | `DataInline`, `DataMember`, `DataSelf` | `DataInlineUnit`, `DataMemberUnit`, `DataSelfUnit` |

### Key Methods

| Method | Purpose |
|--------|---------|
| `GetRequiredService<T>()` | Resolve service (builds provider if needed) |
| `BuildServiceProvider()` | Explicitly build provider |
| `ValidateServicesAsync()` | Validate attribute-based services |
| `GetData(path)` | Get test data file content |
| `AddToConfiguration(object)` | Add config before resolution |

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

| Feature | Description |
|---------|-------------|
| Auto PostgreSQL container | Starts, injects connection strings, applies migrations |
| `PostgresIsolated` | Exclusive container per test (no sharing) |
| `EnsureDatabaseAsync()` | Quick prototyping without manual migrations |
| RabbitMQ support | `ContainerContext.RabbitMq` |
| `Reuse = true` | Keep container across runs |

### Auto-Managed Test Flags

| Flag | Auto-Set | Purpose |
|------|----------|---------|
| `TemporaryApplication` | `true` | Prevents collision with local dev |
| `DrnTestContextEnabled` | `true` | Signals test environment |

---

## ApplicationContext

WebApplicationFactory wrapper for API testing:

```csharp
[Theory]
[DataInline]
public async Task ApiTest(DrnTestContext context, ITestOutputHelper outputHelper)
{
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(outputHelper);
    
    var endpoint = Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern;
    var forecasts = await client.GetFromJsonAsync<WeatherForecast[]>(endpoint);
    forecasts.Should().NotBeEmpty();
}
```

Features: Syncs DrnTestContext with WebApplicationFactory, shares service provider, auto PostgreSQL + migrations, authenticated HTTP client.

---

## FlurlHttpTest Integration

Mock external HTTP requests:

```csharp
context.FlurlHttpTest.RespondWith("{ \"status\": \"ok\" }", 200);
// ... resolve and call service ...
context.FlurlHttpTest.ShouldHaveCalled("https://api.example.com/*").Times(1);
```

---

## Data Attributes

```csharp
// Inline data + auto-fixture
[DataInline(99, "test")]
public void Test(DrnTestContext ctx, int value, string name, Guid auto) { }

// Member data
[DataMember(nameof(TestData))]
public void Test(DrnTestContext ctx, int value) { }
public static IEnumerable<object[]> TestData => new[] { new object[] { 1 }, new object[] { 2 } };

// Self-contained (DrnTestContext variant)
public class MyData : DataSelfAttribute { public MyData() { AddRow(1, "first"); } }

// Self-contained (DrnTestContextUnit variant — use DataSelfContextAttribute)
public class MyUnitData : DataSelfContextAttribute { public MyUnitData() { AddRow(1, "first"); } }

// Auto-mocking — interface parameters auto-created by NSubstitute
[DataInline]
public void Test(DrnTestContext context, IMyService mock)
{
    mock.GetValue().Returns(42);
    context.ServiceCollection.AddScoped<IMyService>(_ => mock);
}
```

### JSON Contract Testing

```csharp
var original = new MyDto { Name = "test", Value = 42 };
var roundTripped = original.ValidateObjectSerialization();
roundTripped.Name.Should().Be("test");
```

| Attribute | Context | Purpose |
|-----------|---------|---------|
| `[FactDebuggerOnly]` | - | Run only when debugger attached |
| `[TheoryDebuggerOnly]` | - | Theory only when debugger attached |

---

## Providers

| Provider | Usage | Source |
|----------|-------|--------|
| `SettingsProvider.GetAppSettings()` | Load IAppSettings | `Settings/` folder |
| `DataProvider.Get("file.json")` | Load test data | `Data/` folder |
| `CredentialsProvider.GenerateCredentials()` | Test credentials | Deterministic generation |

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
</Project>
```

**Data file copy rules** (add if using `Settings/` or `Data/` folders):
```xml
<ItemGroup>
    <None Update="Settings\appsettings.json"><CopyToOutputDirectory>Always</CopyToOutputDirectory></None>
    <None Update="Data\*.txt"><CopyToOutputDirectory>Always</CopyToOutputDirectory></None>
</ItemGroup>
```

### DTT Philosophy: The Pit of Success

DTT minimizes **cognitive cost** and **willpower depletion**. When testing requires high activation energy (wiring dependencies, managing containers, writing boilerplate), developers avoid writing tests. DTT wraps all rigor (containers, migrations, DI, auto-mocking) into a single attribute (`[DataInline]`), making the **Right Thing** accessible via the **Lowest Effort** action. Type `dtt` snippet to scaffold.

---

## Related Skills

- [overview-drn-testing.md](../overview-drn-testing/SKILL.md) - Testing philosophy
- [test-unit.md](../test-unit/SKILL.md) - Unit test patterns
- [test-integration.md](../test-integration/SKILL.md) - Integration patterns
- [test-performance.md](../test-performance/SKILL.md) - Performance testing
- [drn-entityframework.md](../drn-entityframework/SKILL.md) - Database testing

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
