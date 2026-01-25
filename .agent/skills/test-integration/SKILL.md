---
name: test-integration
description: Integration testing suite using Testcontainers, WebApplicationFactory, and xUnit
---

# DRN.Test.Integration Overview

> Integration testing suite using Testcontainers, WebApplicationFactory, and xUnit.

## Core Concepts
- **DrnTestContext**: Primary test fixture.
- **ContainerContext**: Manages docker containers (Postgres, RabbitMQ, etc.).
- **ApplicationContext**: Manages `WebApplicationFactory` for E2E tests.

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