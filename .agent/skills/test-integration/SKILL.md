---
name: test-integration
description: Use when choosing or reviewing integration tests for API pipelines, database behavior, external dependencies, containers, serialization, middleware, or end-to-end component boundaries.
last-updated: 2026-11-13
difficulty: basic
---

# Integration Testing

> Portable integration-test router. Load repository-profile testing rules and scoped framework testing skills first when present.

## Choose the Narrowest Honest Integration

| Need | Use | Skill |
|---|---|---|
| Controllers, endpoints, auth, middleware, serialization, route bindings | API or web-application factory test | [test-integration-api](../test-integration-api/SKILL.md) |
| Repositories, ORM mapping, SQL, interceptors, transactions, concurrency | Database-backed component test | [test-integration-db](../test-integration-db/SKILL.md) |
| Outbound HTTP behavior | HTTP client test double at transport boundary | [test-integration-api](../test-integration-api/SKILL.md) |
| Message bus, queue, cache, object storage | Real container or approved local emulator | Repository-specific skill |

Use parameterized tests when the setup and assertions are genuinely the same across rows. Request heavyweight fixtures or containers only when the test needs them.

## Setup Rules

- API/E2E: start the application through the repository's approved test host and bind external dependencies through test configuration.
- DB/component: register only the services under test and apply migrations/schema setup explicitly.
- Run integration tests only after unit tests pass and the user explicitly permits integration execution.
- Do not merge structurally different behaviors merely to reduce test count.

## Composed Decisions

Apply [MC/DC fixture rules](../test-unit/SKILL.md#composed-decisions) to composed authorization, tenant isolation, soft-delete, filtering, validation, and feature-gate decisions. Validate fixture truth tables before invoking the boundary. Assert the exact result and cover distinct integration interactions.

## Related

- [test-integration-api](../test-integration-api/SKILL.md)
- [test-integration-db](../test-integration-db/SKILL.md)
- [test-unit](../test-unit/SKILL.md)
