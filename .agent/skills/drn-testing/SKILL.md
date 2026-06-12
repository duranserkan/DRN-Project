---
name: drn-testing
description: "DRN.Framework.Testing - canonical DTT attribute/context matrix with DrnTestContext, DrnTestContextUnit, ContainerContext, ApplicationContext, DataInline/DataMember/DataSelf, MTP command guidance, AwesomeAssertions, AutoFixture, NSubstitute, Testcontainers, and xUnit v3. Keywords: testing, dtt, drntest-context, applicationcontext, testcontainers, mtp, xunit, data-attributes, unit-testing, integration-testing"
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~1.6K
---

# DRN.Framework.Testing

> Canonical DTT (Duran's Testing Technique) guidance for choosing attributes, contexts, and test commands.

Shared testing settings, container defaults, and docs-sync rules live in the [DRN Framework Maintenance Reference](../overview-drn-framework/SKILL.md#drn-framework-maintenance-reference).

## When to Apply

- Selecting `[Fact]`, `DataInline*`, `DataMember*`, or `DataSelf*` patterns
- Writing unit, integration, API, or database component tests
- Using `DrnTestContext`, `DrnTestContextUnit`, `ContainerContext`, `ApplicationContext`, or `FlurlHttpTest`
- Explaining how DRN test projects run under Microsoft Testing Platform (MTP)

## DTT Attribute/Context Matrix

| Scenario | Attribute | Context parameter | Auto-generated params | Extra capabilities |
|---|---|---|---|---|
| No inline data, generated params, or context | `[Fact]` | none | no | simplest test shape |
| Unit rows or generated params | `[Theory]` + `[DataInlineUnit]` | optional `DrnTestContextUnit` first | yes | DI, config, data files, service validation |
| Unit member data | `[Theory]` + `[DataMemberUnit]` | optional `DrnTestContextUnit` first | yes | complex data from static member |
| Unit self-contained data | `[Theory]` + custom `DataSelfUnitAttribute` | optional `DrnTestContextUnit` first | yes | complex rows declared in an attribute class |
| Integration rows or generated params | `[Theory]` + `[DataInline]` | optional `DrnTestContext` first | yes | DI, config, data files, Testcontainers, `ApplicationContext`, `FlurlHttpTest` |
| Integration member data | `[Theory]` + `[DataMember]` | optional `DrnTestContext` first | yes | complex data from static member |
| Integration self-contained data | `[Theory]` + custom `DataSelfAttribute` | optional `DrnTestContext` first | yes | complex rows declared in an attribute class |

Rules:

- Request `DrnTestContext` / `DrnTestContextUnit` only when the test uses context services, configuration, data providers, containers, service validation, or app bootstrapping.
- Use `DataInlineUnit` for pure logic and isolated services. Use `DataInline` when real dependencies make the signal more honest.
- The context must be the first parameter when requested; attribute data follows; AutoFixture/NSubstitute fill missing parameters.
- Assertions use `AwesomeAssertions`.
- Unit self-data derives from `DataSelfUnitAttribute`.

## Minimal Patterns

```csharp
[Fact]
public void Trim_Should_Remove_Outer_Whitespace()
{
    "  Duran  ".Trim().Should().Be("Duran");
}

[Theory]
[DataInlineUnit(2, 3, 5)]
[DataInlineUnit(-1, -2, -3)]
public void Add_Should_Return_Correct_Sum(int a, int b, int expected)
{
    (a + b).Should().Be(expected);
}

[Theory]
[DataInlineUnit]
public void Service_Should_Use_Mocked_Dependency(DrnTestContextUnit context, IDependency dependency)
{
    dependency.GetValue().Returns(42);
    context.ServiceCollection.AddScoped<IDependency>(_ => dependency);
    context.ServiceCollection.AddScoped<MyService>();

    context.GetRequiredService<MyService>().Calculate().Should().Be(42);
}

public sealed class CategoryRows : DataSelfUnitAttribute
{
    public CategoryRows()
    {
        AddRow("dotnet");
        AddRow("security");
    }
}
```

```csharp
[Theory]
[DataInline]
public async Task Repository_Should_Persist_Entity(DrnTestContext context)
{
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();

    var repository = context.GetRequiredService<ICategoryRepository>();
    var category = new Category("dotnet");

    await repository.CreateAsync(category);

    category.EntityId.Should().NotBe(Guid.Empty);
}

[Theory]
[DataInline]
public async Task Endpoint_Should_Return_Data(DrnTestContext context, ITestOutputHelper output)
{
    var client = await context.ApplicationContext.CreateClientAsync<SampleProgram>(output);
    var result = await client.GetFromJsonAsync<WeatherForecast[]>(
        Get.Endpoint.Sample.WeatherForecast.Get.RoutePattern);

    result.Should().NotBeEmpty();
}
```

## Context Capabilities

| Context | Use for | Notable members |
|---|---|---|
| `DrnTestContextUnit` | unit tests without containers or full app startup | `ServiceCollection`, `GetRequiredService<T>()`, `BuildConfigurationRoot()`, `GetData()`, `ValidateServicesAsync()` |
| `DrnTestContext` | integration tests | all unit capabilities plus `ContainerContext`, `ApplicationContext`, `FlurlHttpTest` |
| `ContainerContext` | real dependencies | `Postgres.ApplyMigrationsAsync()`, `Postgres.Isolated`, `RabbitMq`, `BindExternalDependenciesAsync()` |
| `ApplicationContext` | API/E2E tests | `CreateClientAsync<TProgram>()`, `CreateApplicationAndBindDependenciesAsync<TProgram>()`, `LogToTestOutput()` |
| `FlurlHttpTest` | outbound HTTP mocks | `ForCallsTo(...).RespondWithJson(...)`, `ShouldHaveCalled(...)` |

## Consolidation Rule

Prefer one readable test that exercises a coherent flow over many duplicate tests with the same setup. Parameterize identical bodies with multiple data rows. In integration tests, continue the same flow when assertions share container setup, migrations, or service registration. Do not combine structurally different behaviors only to reduce count.

## MTP Commands

Repository rule: do not build or run tests unless the user explicitly allows it. When allowed, prefer commands from `.agent/repository-profile.md`; otherwise discover test project paths from the filesystem or CI. Use project commands only; never use `.slnx` for test execution.

```bash
# Unit tests first
dotnet run --project <unit-test-csproj>

# Filter a unit class or method
dotnet run --project <unit-test-csproj> -- --filter-class Fully.Qualified.TestClass
dotnet run --project <unit-test-csproj> -- --filter-method Fully.Qualified.TestMethod

# Integration tests only after unit tests pass
dotnet run --project <integration-test-csproj>

# Filter integration tests
dotnet run --project <integration-test-csproj> -- --filter-class Fully.Qualified.IntegrationTestClass

# Performance only on explicit request
dotnet run -c Release --project <performance-test-csproj> -- --filter-method Fully.Qualified.PerformanceTestClass.Run_Benchmarks
```

## Test Project Essentials

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <OutputType>Exe</OutputType>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
</PropertyGroup>
```

If a test uses `Settings/` or `Data/`, copy those files to output. Global usings should include `AwesomeAssertions`, `NSubstitute`, xUnit v3, `DRN.Framework.Testing.Contexts`, `DRN.Framework.Testing.DataAttributes`, and `Microsoft.Extensions.DependencyInjection`.

## Related Skills

- [overview-drn-testing](../overview-drn-testing/SKILL.md) - DTT philosophy and test-type choice
- [test-unit](../test-unit/SKILL.md) - unit-specific patterns
- [test-integration](../test-integration/SKILL.md) - integration routing
- [test-integration-api](../test-integration-api/SKILL.md) - API/E2E patterns
- [test-integration-db](../test-integration-db/SKILL.md) - database component patterns
- [test-performance](../test-performance/SKILL.md) - benchmark/load-test guidance
