---
name: drn-testing
description: "Choose DRN unit and integration data attributes, test contexts, fixtures, and MTP entry points."
last-updated: 2026-11-13
difficulty: intermediate
---

# DRN.Framework.Testing

Use the installed `DRN.Framework.Testing` package README for provider APIs and project setup. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

## DTT Attribute/Context Matrix

| Data source | Unit | Integration |
|---|---|---|
| Inline rows or generated parameters | `[DataInlineUnit]` | `[DataInline]` |
| Static member | `[DataMemberUnit]` | `[DataMember]` |
| Custom attribute class | `DataSelfUnitAttribute` | `DataSelfAttribute` |

Use `[Theory]` with these attributes. Use `[Fact]` when no data, generation or context is needed.

- Request a context only for services, settings, files, service validation, containers or app startup.
- Unit projects use `DrnTestContextUnit`. Full `DrnTestContext` belongs in integration projects and adds real dependencies and application hosting.
- Put the optional context first, followed by attribute data. AutoFixture/NSubstitute fill missing parameters.
- For one null inline value, use an explicit scalar cast such as `[DataInlineUnit((string?)null)]`. Bare `null` or `null!` can bind to the entire params array instead of a single data element.
- Interface/abstract parameters auto-mocked by NSubstitute can replace matching `ServiceCollection` registrations. Use those substitutes for dependencies, not for concrete convenience methods whose implementation is the behavior under test.
- Assertions use `AwesomeAssertions`.
- For temporal ID filters, cover inclusive/exclusive edges, equal and distinct endpoints, reversed ranges and ordering transitions. Keep deterministic comparisons in unit tests. Use database integration for provider translation or query behavior.
- Never bypass runtime invariants with environment branches for tests. Use explicit `TemporaryApplication`, `SkipValidation` or dedicated contexts.

## Regression fixtures

For an HTTP integration test, substitute the application's own host entry point and route:

```csharp
[Theory]
[DataInline("/health")]
public async Task Health_endpoint_succeeds(DrnTestContext context, string route)
{
    using var client = await context.ApplicationContext.CreateClientAsync<AppProgram>();
    using var response = await client.GetAsync(route);
    response.EnsureSuccessStatusCode();
}
```

- Isolate process-wide ID startup and first-use checks in fresh support processes. Preserve production statics and the machine clock.
- Analyzer fixtures need real logging and EF references. Pair permitted schema, constant-key and substitute-verification cases with unsafe writes and lookalike APIs. Cover exact comment names, statement boundaries, and valid, malformed and unused acknowledgements. Keep suppressions narrow and explained.
- Share analyzer/browser catalog fixtures. Cover inherited and malformed catalogs, compilation isolation, deployment and code fixes. Verify runtime rejection of an invalid replacement catalog. Backend fixtures must classify dynamic values explicitly. Annotate fixtures and use named models for matching anonymous properties. See [classification rules](../drn-sharedkernel/SKILL.md#attributes).
- For diagnostic capture, cover native and extension dictionary APIs, dynamic keys, protected descendants, detached copies and inherited/interface classifications. Unrelated same-named members must not inherit classification. Verify getter behavior, ignored members, serialization failure recovery and credential suppression. Keep implementation-specific cases with their source tests.

## Context Capabilities

Use `SettingsProvider.Development()` for AppId 0 or `Development<TApp>()` for the declared AppId. Both default AppInstanceId to 0 and return disposable settings. Optional objects override defaults. AppId must match the selected partition.

| Context | Use for | Notable members |
|---|---|---|
| `DrnTestContextUnit` | unit tests without containers or full app startup | `ServiceCollection`, `GetRequiredService<T>()`, `BuildConfigurationRoot()`, `GetData()`, `MethodContext.GetTempPath()`, `GetTempPath()`, `ValidateServicesAsync()` |
| `DrnTestContext` | integration tests | all unit capabilities plus `ContainerContext`, `ApplicationContext`, `FlurlHttpTest` |
| `ContainerContext` | real dependencies | `Postgres.ApplyMigrationsAsync()`, `Postgres.Isolated`, `RabbitMq`, `BindExternalDependenciesAsync()` |
| `ApplicationContext` | API/E2E tests | `CreateClientAsync<T>()`, `CreateClientForServiceAsync<T>()`, `CreateApplicationAndBindDependenciesAsync<T>()`, `MapAddress<T>()`, in-memory routing via `ApplicationContextRouterHandler`, automatic debugger-gated logging through `Xunit.TestContext.Current.TestOutputHelper` |
| `FlurlHttpTest` | outbound HTTP mocks | `ForCallsTo(...).RespondWithJson(...)`, `ShouldHaveCalled(...)` |

`DrnTestContext` owns its providers. `ApplicationContext` owns one active factory per application type and routes service calls in memory. Re-creating a type replaces only its instance and routes, using current configuration. Failed factory shutdown can be retried during disposal.

`ApplicationContext.CreateClientAsync<TProgram>` has one signature with the optional first parameter `bool https = false`. `https: true` defaults to `https://localhost`. Omitted or false defaults to HTTP. Explicit `clientOptions` take precedence over the flag and remain unchanged. Supply `outputHelper`, `clientOptions`, and `additionalAddresses` by name when omitting the flag.

## Consolidation Rule

Prefer one readable test that exercises a coherent flow over many duplicate tests with the same setup. Parameterize identical bodies with multiple data rows. In integration tests, continue the same flow when assertions share container setup, migrations, or service registration. Do not combine structurally different behaviors only to reduce count.

## Composed Decision Coverage

Use [Modified Condition/Decision Coverage (MC/DC)](../test-unit/SKILL.md#composed-decisions) for authorization, tenant isolation, soft-delete, repository filters, validation chains and feature gates. Assert exact result membership. Add interaction cases only when independent pairs cannot cover the behavior.

## WebApplicationFactory Entry Points

`WebApplicationFactory<TProgram>` should point at a real hosted program type from the application or a non-test support assembly. Keep reusable disposable integration-test host programs in an SDK Web support project with `IsTestProject=false`. Discover its path from the repository profile or project references, and keep the integration-test project focused on assertions. Do not define custom hosted `Program` entry points inside the MTP test executable. `DrnWebApplicationFactory<TProgram>` automatically resolves and binds secondary `IDrnProgram` entry points in multi-program support assemblies, allowing any program in the assembly to be hosted in-memory via `ApplicationContext.CreateClientAsync<TProgram>()`.

## Razor Rendering

For Razor rendering helpers, cover `_ViewStart`, layouts and standalone pages. Verify scope disposal on successful and failing renders.

## MTP commands and setup

Find the test project and target framework in the application's project files, profile or CI. Follow its command approval rules. Run MTP projects individually. Unit tests precede integration tests, which need explicit permission. Benchmarks and load tests need an explicit request.

Use an executable non-packable test project with `IsTestProject` and `UseMicrosoftTestingPlatformRunner` enabled. Copy fixture Settings and Data to the output when used. The Testing README owns the sample project and imports.

## Related Skills

- [overview-drn-testing](../overview-drn-testing/SKILL.md) - DTT philosophy and test-type choice
- [test-unit](../test-unit/SKILL.md) - unit-specific patterns
- [test-integration](../test-integration/SKILL.md) - integration routing
- [test-integration-api](../test-integration-api/SKILL.md) - API/E2E patterns
- [test-integration-db](../test-integration-db/SKILL.md) - database component patterns
- [test-performance](../test-performance/SKILL.md) - benchmark/load-test guidance
