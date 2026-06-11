---
name: overview-ddd-architecture
description: Use when navigating or reviewing Domain-Driven Design architecture, layered boundaries, project placement, dependency direction, bounded contexts, domain model responsibilities, or DDD refactors.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~1.2K
---

# DDD Architecture

> Portable Domain-Driven Design layer guidance. Load the repository profile and any framework-specific domain skills before applying concrete naming or base-type rules.

## When to Apply

- Starting a new application, module, or bounded context.
- Deciding where to place code.
- Reviewing dependency direction.
- Separating business rules from I/O, UI, persistence, or framework details.

## Layer Structure

```text
Presentation  ->  Application  ->  Domain  <-  Infrastructure
    UI/API         Orchestration     Core        Data/External
```

Dependency rule: dependencies flow inward. Domain owns business concepts and should not depend on Presentation, Infrastructure, or external delivery mechanisms. Infrastructure implements persistence and external integrations behind contracts owned by the inner layers.

## Layer Responsibilities

| Concern | Application Layer | Domain Layer |
|---------|-------------------|--------------|
| Owns | Use-case orchestration, transactions, coordination, DTO mapping | Invariants, entities, value objects, domain events, business decisions |
| Avoids | Business-rule decisions hidden in handlers | I/O, HTTP, database calls, framework-specific workflow |
| Example | Load order, ask domain to pay, save result | Decide whether order can be paid |

## Common Layers

### Domain

Place here:

- Entities, aggregate roots, value objects.
- Domain events and domain exceptions.
- Repository or port interfaces when following ports/adapters.
- Pure domain services.

Never place here:

- ORM mappings, SQL, HTTP clients, controllers, Razor/view code, or deployment configuration.

### Contract

Place here when the repository uses a contract layer:

- DTOs, API models, shared enums, value models, and public message contracts.

Never place here:

- Persistence entities, domain services, repository implementations, or infrastructure clients.

### Application

Place here:

- Use-case handlers, application services, validation, transactions, authorization checks that coordinate a use case, and DTO/domain mapping.

Never place here:

- Entity definitions, database implementation details, UI rendering, or hidden domain invariants.

### Infrastructure

Place here:

- ORM contexts, migrations, repository implementations, external service clients, filesystem adapters, message-bus adapters, and cache adapters.

Never place here:

- Business decisions, domain events as concepts, or UI rendering.

### Presentation

Place here:

- Controllers, minimal APIs, Razor Pages, views, page models, endpoint configuration, static assets, and request/response concerns.

Never place here:

- Business rules, direct persistence access, or repository implementations.

## Naming And Registration

Use repository conventions first. Common conventions include:

- `*.Domain`, `*.Application`, `*.Infra` or `*.Infrastructure`, `*.Contract`, `*.Hosted` or `*.Web`.
- Module registration methods such as `Add<Module>Services`.
- One bounded context per module family unless the repository profile says otherwise.

## Review Prompts

- Is the business rule in Domain rather than a controller, handler, query, or migration?
- Does Infrastructure implement interfaces instead of leaking concrete clients inward?
- Are external contracts free of persistence-only fields and internal identifiers?
- Does a new abstraction remove real duplication or protect a stable boundary?
- Did the repository profile declare framework-specific entity, DTO, ID, or DI rules that must be applied?

## Related Skills

- [overview-repository-structure.md](../overview-repository-structure/SKILL.md) - Repository navigation conventions.
- [basic-code-review.md](../basic-code-review/SKILL.md) - Review gate.
