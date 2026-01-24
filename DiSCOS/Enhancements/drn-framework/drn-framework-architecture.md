## DRN.Framework Architecture

> Essential architecture knowledge for working with DRN.Framework projects.

---

## Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| Runtime | .NET 9+ | Platform |
| Web | ASP.NET Core (Kestrel) | Hosting, security headers, CSP |
| ORM | Entity Framework Core + PostgreSQL | Persistence |
| DI | Attribute-based assembly scanning | Convention-driven registration |
| Testing | xUnit + Testcontainers + NSubstitute + AutoFixture | Full testing pyramid |
| Logging | NLog (structured) | Request-scoped observability |
| Messaging | MassTransit (planned) | Event-driven architecture |

---

## Layered Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Hosted (Entry Point)                      │
│  Program : DrnProgramBase<Program> → Web host, DI config    │
├─────────────────────────────────────────────────────────────┤
│                    Application Layer                         │
│  Use cases, orchestration, DTOs → No framework deps          │
├─────────────────────────────────────────────────────────────┤
│                    Domain Layer                              │
│  SourceKnownEntity, AggregateRoot, DomainEvent              │
├─────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                      │
│  DrnContext<T>, Repositories, External integrations         │
├─────────────────────────────────────────────────────────────┤
│                    Contract Layer                            │
│  Shared DTOs, API contracts, Events                          │
├─────────────────────────────────────────────────────────────┤
│                    Utils Layer                               │
│  Cross-cutting: IAppSettings, IScopedLog, ISourceKnownIdUtils│
└─────────────────────────────────────────────────────────────┘
```

---

## Framework Package Hierarchy

```
DRN.Framework.SharedKernel  ← Base (domain primitives, exceptions)
       ↑
DRN.Framework.Utils         ← Cross-cutting (DI, settings, logging, IDs)
       ↑
DRN.Framework.EntityFramework ← Persistence (DrnContext, conventions)
       ↑
DRN.Framework.Hosting       ← Web host (security, middleware, endpoints)
       ↑
DRN.Framework.Testing       ← Test infrastructure (contexts, containers)
```

**Dependency rule**: Lower layers have no knowledge of higher layers.

---

## Project Structure Convention

```
Solution/
├── [App].Hosted/          # Entry point, Program.cs, controllers
├── [App].Application/     # Use cases, services, handlers
├── [App].Domain/          # Entities, aggregates, domain events
├── [App].Infra/           # DbContexts, repositories, external APIs
├── [App].Contract/        # DTOs, API contracts, shared models
├── [App].Utils/           # App-specific utilities
└── [App].Test.*/          # Unit, Integration, Performance tests
```

---

## Domain-Driven Design Elements

| DDD Concept | DRN Implementation |
|-------------|-------------------|
| **Entity** | `SourceKnownEntity` — ID + lifecycle events |
| **Aggregate Root** | `AggregateRoot : SourceKnownEntity` |
| **Value Object** | Immutable records/classes |
| **Domain Event** | `DomainEvent` → `GetCreatedEvent()`, `GetModifiedEvent()` |
| **Repository** | `IRepository<T>` pattern via `DrnContext.Set<T>()` |
| **Entity ID** | `[EntityType(n)]` attribute → embedded metadata in ID |

---

## Key Architectural Patterns

### 1. Attribute-Based DI (Convention over Configuration)
```csharp
[Scoped<IOrderService>]    // Auto-registered on assembly scan
public class OrderService : IOrderService { }

[Config]                    // Binds appsettings section
public class OrderSettings { }
```

### 2. Source-Known Identity
IDs encode: `timestamp + appId + instanceId + sequence`
- Globally unique across distributed systems
- Creation time embedded, no DB round-trip for ordering

### 3. Request Scope Context
```csharp
ScopeContext.UserId        // Current user
ScopeContext.Log           // Request-scoped structured log
ScopeContext.Settings      // IAppSettings
```

### 4. Security-First Hosting
- CSP with nonce-based scripts
- HSTS, X-Frame-Options, X-Content-Type-Options
- MFA enforcement via authorization policies
- Cookie policy: SameSite=Strict, Secure, HttpOnly

---

## Testing Architecture

| Context | Purpose |
|---------|---------|
| `DrnTestContextUnit` | Unit tests with mocking |
| `DrnTestContext` | Integration tests with real services |
| `ApplicationContext` | Full WebApplicationFactory integration |
| `ContainerContext` | Testcontainers (Postgres, RabbitMQ) |

**Pattern**: First parameter `DrnTestContext` + `[DataInline]` → auto-inject mocks & fixtures

---

## External Dependencies

| Dependency | Usage |
|------------|-------|
| PostgreSQL | Primary database via EF Core |
| Docker | Development & testing (Testcontainers) |
| NLog | Structured logging |
| Flurl | HTTP client with testing support |
| Blake3 | High-performance hashing |

---
