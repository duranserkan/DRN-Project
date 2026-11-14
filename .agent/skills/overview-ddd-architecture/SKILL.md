---
name: overview-ddd-architecture
description: Use when navigating or reviewing Domain-Driven Design architecture, layered boundaries, project placement, dependency direction, bounded contexts, domain model responsibilities, or DDD refactors.
last-updated: 2026-11-13
difficulty: intermediate
---

# DDD Architecture

> Portable Domain-Driven Design layer guidance. Load the repository profile and any framework-specific domain skills before applying concrete naming or base-type rules.

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

| Layer | Place here | Keep out |
|---|---|---|
| Domain | Entities, aggregates, value objects, domain events/exceptions, pure services, port interfaces | ORM mappings, SQL, HTTP clients, UI, deployment configuration |
| Contract, when present | DTOs, API models, shared enums/value models, public messages | Persistence entities, domain services, repositories, infrastructure clients |
| Application | Use-case handlers, validation, transactions, coordinating authorization, DTO mapping | Entity definitions, database implementation, UI, hidden invariants |
| Infrastructure | ORM contexts, migrations, repositories, external clients, filesystem/message/cache adapters | Business decisions, domain event definitions, UI |
| Presentation | Controllers, APIs, Razor/views, page models, endpoints, assets, request/response handling | Business rules, direct persistence, repository implementations |

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
