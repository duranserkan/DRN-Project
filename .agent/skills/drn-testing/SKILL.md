---
name: drn-testing
description: "Choose DRN unit and integration data attributes, test contexts, fixtures, and MTP entry points."
last-updated: 2026-11-07
difficulty: intermediate
---

# DRN.Framework.Testing

Use the installed `DRN.Framework.Testing` package README for provider APIs and project setup. [Locate the package](../overview-drn-framework/SKILL.md#documentation-and-source-references)

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

- Request `DrnTestContext` / `DrnTestContextUnit` only when the test uses context services, configuration, data providers, containers, service validation, or app bootstrapping.
- Use `DataInlineUnit` for pure logic and isolated services. Use `DataInline` when real dependencies make the signal more honest.
- Never use `DrnTestContext` in unit-test projects. Use `DrnTestContextUnit` there and place full `DrnTestContext` coverage in integration-test projects discovered from the current repository.
- The context must be the first parameter when requested; attribute data follows; AutoFixture/NSubstitute fill missing parameters.
- For one null inline value, use an explicit scalar cast such as `[DataInlineUnit((string?)null)]`. Bare `null` or `null!` can bind to the entire params array instead of a single data element.
- Interface/abstract parameters auto-mocked by NSubstitute can replace matching `ServiceCollection` registrations. Use those substitutes for dependencies, not for concrete convenience methods whose implementation is the behavior under test.
- Assertions use `AwesomeAssertions`.
- For coarse-precision temporal ID filters, cover inclusive/exclusive payload edges, equal and distinct endpoints, reversed ranges, and ordering transitions. Keep deterministic ID comparisons in unit tests; use database integration only when provider translation or query-plan behavior is under test.
- **Environment Invariants & Test Flags**: Never bypass framework/app invariants by branching on environment checks (such as `IsDevelopment()`) for tests; configure explicit test/temporary flags (`TemporaryApplication`, `SkipValidation`) or dedicated test contexts instead.

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

- For ID startup and first-use checks, use fresh support processes. Inspect `SourceKnownGenerationTime.Policy` without freezing it in the probe. Use isolated unit state for rounding, drift, retries and concurrency. Preserve production statics and the machine clock.
- Register real logging and EF migration references for analyzer tests. Pair accepted schema expressions, constant keys and direct substitute verification calls with migration data, unsafe writes, lookalikes and ordinary calls. Cover exact comment names, statement boundaries, malformed and unused acknowledgements. Keep suppressions narrow and explained.
- Use shared analyzer/browser catalog fixtures and separate backend cases for explicit classification. Exercise reference-assembly inheritance, isolation between compilations, malformed catalogs, copy/publish boundaries and code fixes. An invalid replacement file proves runtime revalidation. Annotate fixture properties and use named models for matching anonymous properties. See [classification rules](../drn-sharedkernel/SKILL.md#attributes).
- Test native and extension dictionary APIs, dynamic keys, exact classifications, configured paths, protected descendants and detached copies. Include inherited and explicit interface implementations, unrelated same-named members, classified getters and ignored members. Keep positive/negative cache assertions independent across parallel tests. Verify escaped Unicode reconstruction, repeated and reentrant sizing, and recovery after serialization failure. Preserve credential suppression in external-dependency logs.

## Context Capabilities

Use `SettingsProvider.Development()` for AppId 0 or `Development<TApp>()` for the declared AppId. Both default AppInstanceId to 0 and return disposable settings. Optional objects override defaults; AppId must match the selected partition.

| Context | Use for | Notable members |
|---|---|---|
| `DrnTestContextUnit` | unit tests without containers or full app startup | `ServiceCollection`, `GetRequiredService<T>()`, `BuildConfigurationRoot()`, `GetData()`, `MethodContext.GetTempPath()`, `GetTempPath()`, `ValidateServicesAsync()` |
| `DrnTestContext` | integration tests | all unit capabilities plus `ContainerContext`, `ApplicationContext`, `FlurlHttpTest` |
| `ContainerContext` | real dependencies | `Postgres.ApplyMigrationsAsync()`, `Postgres.Isolated`, `RabbitMq`, `BindExternalDependenciesAsync()` |
| `ApplicationContext` | API/E2E tests | `CreateClientAsync<T>()`, `CreateClientForServiceAsync<T>()`, `CreateApplicationAndBindDependenciesAsync<T>()`, `MapAddress<T>()`, in-memory routing via `ApplicationContextRouterHandler`, automatic debugger-gated logging through `Xunit.TestContext.Current.TestOutputHelper` |
| `FlurlHttpTest` | outbound HTTP mocks | `ForCallsTo(...).RespondWithJson(...)`, `ShouldHaveCalled(...)` |

`DrnTestContext` owns the bootstrap and migration providers it builds. `ApplicationContext` manages one active `WebApplicationFactory` per application type and routes outbound HTTP calls between hosted services in-memory via `ApplicationContextRouterHandler`. Re-creating an application instance disposes and updates that specific factory and route bindings. Sequential applications rebuild context-owned services from current configuration, and a factory whose shutdown fails remains available for disposal retry.

`ApplicationContext.CreateClientAsync<TProgram>` has one signature with the optional first parameter `bool https = false`. `https: true` defaults to `https://localhost`; omitted or false defaults to HTTP. Explicit `clientOptions` take precedence over the flag and remain unchanged. Supply `outputHelper`, `clientOptions`, and `additionalAddresses` by name when omitting the flag.

## Consolidation Rule

Prefer one readable test that exercises a coherent flow over many duplicate tests with the same setup. Parameterize identical bodies with multiple data rows. In integration tests, continue the same flow when assertions share container setup, migrations, or service registration. Do not combine structurally different behaviors only to reduce count.

## Composed Decision Coverage

For AND/OR predicates where each condition must independently affect the result, use Modified Condition/Decision Coverage (MC/DC). Paired fixtures must differ in only the target condition and must make the overall decision change. For an AND predicate, keep every other condition true while the target changes; for an OR predicate, keep every other condition false while the target changes. Include a positive fixture for the full expression, validate the fixture truth table before exercising the system, and assert exact result membership instead of relying only on a shared predicate assertion.

Apply this at decision boundaries such as authorization, tenant isolation, soft-delete, repository filters, validation chains, and feature gates. Add pairwise or exhaustive cases only when condition interactions produce behavior that independent pairs do not cover.

## WebApplicationFactory Entry Points

`WebApplicationFactory<TProgram>` should point at a real hosted program type from the application or a non-test support assembly. Keep reusable disposable integration-test host programs in an SDK Web support project with `IsTestProject=false`. Discover its path from the repository profile or project references, and keep the integration-test project focused on assertions. Do not define custom hosted `Program` entry points inside the MTP test executable. `DrnWebApplicationFactory<TProgram>` automatically resolves and binds secondary `IDrnProgram` entry points in multi-program support assemblies, allowing any program in the assembly to be hosted in-memory via `ApplicationContext.CreateClientAsync<TProgram>()`.

## UnsafeAccessors in Test Utilities

When test helpers, assertion extensions, or mocks need access to private or internal framework/library state (such as `HttpTest._filteredSetups` or `WebApplication` pipeline details), always prefer `[UnsafeAccessor]` (`System.Runtime.CompilerServices`) over reflection (`Type.GetField`, `Type.GetMethod`, `BindingFlags`). Reserve runtime reflection strictly for dynamically discovered types whose compile-time metadata is inaccessible.

## Razor Rendering

For Razor rendering helpers, exercise a real `RazorView` with `_ViewStart` and a layout as well as a standalone page. These templates participate in activation but do not derive from Razor Pages' `Page` type. Keep scope-disposal coverage on both successful and failing renders.

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
