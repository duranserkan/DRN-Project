---
name: test-integration
description: Integration testing overview - Orchestrates API testing (WebApplicationFactory) and database component testing (Testcontainers). Navigation hub for test-integration-api and test-integration-db specialized skills. Keywords: integration-testing, testcontainers, webapplicationfactory, api-testing, database-testing, e2e-testing, dtt
last-updated: 2026-02-15
difficulty: basic
tokens: ~0.5K
---

# DRN.Test.Integration Overview

> Integration testing suite using Testcontainers, WebApplicationFactory, and xUnit.

## Core Concepts
- **DrnTestContext**: Primary test fixture.
- **ContainerContext**: Manages docker containers (Postgres, RabbitMQ, etc.).
- **ApplicationContext**: Manages `WebApplicationFactory` for E2E tests (automatically sets `DrnTestContextEnabled` and `TemporaryApplication` flags).

## Skill Selection

### [API & End-to-End Testing](../test-integration-api/SKILL.md)
**Use when testing Controllers/Endpoints.**
- Uses `CreateClientAsync` (Auto-setup).
- Mocks external HTTP calls via `Flurl`.
- Validates full request pipeline (middleware, auth, etc.).

### [Database Component Testing](../test-integration-db/SKILL.md)
**Use when testing internal components (Repositories, Services) in isolation.**
- **Manual Setup Required**: Must call `AddServices` and `ApplyMigrationsAsync`.
- Best for checking complex SQL, transactions, or concurrency logic without web overhead.

## Test Consolidation

If tests share the same setup and their consolidation creates no semantic or performance issue, they should be unified. Apply when consolidation requires only minimal essential change. Integration tests benefit most — container startup, migrations, and service registration are expensive to repeat.

### Parameterized

When multiple cases share identical test bodies, consolidate into one `[Theory]` with multiple `[DataInline]` rows:

```csharp
[Theory]
[DataInline(AppEnvironment.Development, "ViveLaRépublique", true)]
[DataInline(AppEnvironment.Production, "ViveLaRépublique", true)]
public async Task ConnectionString_Should_Be_Set(DrnTestContext context,
    AppEnvironment environment, string connectionString, bool expected)
{
    context.ServiceCollection.AddSampleInfraServices();
    await context.ContainerContext.Postgres.ApplyMigrationsAsync();
    // Act & Assert using environment + connectionString params
}
```

**Rules**: Last param = expected result · Name covers the dimension, not one case · Comment rows when values aren't obvious · Don't consolidate when test bodies differ structurally.

### Flow

When tests share identical setup (container init, migrations, service registration) and additional assertions can be applied by continuing the existing test flow, unify into a single test. This prevents code duplication, maintenance burden, and redundant setup/teardown cost.

**Reference**: [QAContextTagTests.cs](file:///Users/duranserkankilic/Work/Drn-Project/DRN.Test.Integration/Tests/Sample/Infra/QA/QAContextTagTests.cs) — single test flow that validates entity IDs, JSON model queries, date filters, and materialization interceptor with one shared setup.

## Project Structure
```text
DRN.Test.Integration/
├── Tests/
│   ├── Sample/
│   │   ├── Controller/   # API Tests (End-to-End)
│   │   └── Infra/        # DB/Component Tests (Isolated)
├── TestStartupJob.cs     # One-time global setup (passwords, auth)
└── Usings.cs             # Global Usings (xUnit, FluentAssertions)
```
