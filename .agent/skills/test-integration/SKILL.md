---
name: test-integration
description: Integration testing overview - Orchestrates API testing (WebApplicationFactory) and database component testing (Testcontainers). Navigation hub for test-integration-api and test-integration-db specialized skills. Keywords: integration-testing, testcontainers, webapplicationfactory, api-testing, database-testing, e2e-testing, dtt
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