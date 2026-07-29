---
name: test-integration
description: Use when choosing or reviewing integration tests for API pipelines, database behavior, external dependencies, containers, serialization, middleware, or end-to-end component boundaries.
last-updated: 2026-07-29
difficulty: basic
tokens: ~0.5K
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
- Unit tests run first; integration tests run only after unit tests pass and only when the user explicitly allows test execution.
- Do not merge structurally different behaviors merely to reduce test count.

## Composed Decisions

Use MC/DC when an integration boundary composes conditions that must each affect the result. Paired fixtures must differ only in the target condition and flip the overall decision: hold other conditions true for AND and false for OR. Keep a positive full-expression fixture, validate the fixture truth table before invoking the boundary, and assert exact returned membership. This applies to authorization, tenant isolation, soft-delete, repository filters, validation chains, and feature gates; expand beyond independent pairs only when interactions create distinct behavior.

## Related

- [test-integration-api](../test-integration-api/SKILL.md)
- [test-integration-db](../test-integration-db/SKILL.md)
- [test-unit](../test-unit/SKILL.md)
