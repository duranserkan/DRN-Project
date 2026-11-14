---
name: test-integration-db
description: Use when testing repositories, ORM mapping, SQL queries, migrations, transactions, concurrency, or persistence behavior with a real database or container.
last-updated: 2026-11-13
difficulty: intermediate
---

# Database Component Testing

> Isolated database-backed testing for repositories and persistence configuration. Apply repository-profile database and test-container conventions first.

## When to Apply

Use this when you do not need the full web/API stack: complicated queries, mapping, migrations, transactions, triggers, concurrency, or repository behavior.

## Setup Rules

- Start or bind the database through the repository's approved test fixture.
- Apply migrations or schema setup explicitly when the full application host is not used.
- Register only the infrastructure/application services needed by the component under test.
- Use separate scopes or connections when simulating concurrent users.
- Clean data deterministically between tests.

## Basic Repository Shape

Create an entity through its public contract, persist it, and read it through a separate context or scope. Assert the stored values. A read from the same tracked instance does not prove persistence.

## Concurrency

Verify optimistic concurrency by using separate scopes, contexts, sessions, or connections. Assert the exact conflict behavior the repository exposes, not just the ORM exception type, unless the ORM exception is the public contract.

## Composed Query Predicates

Apply [MC/DC fixture rules](../test-unit/SKILL.md#composed-decisions) to predicates that determine membership. Validate row truth tables before persistence and assert exact returned membership. Cover distinct provider translation and condition interactions.

## Related

- [test-integration](../test-integration/SKILL.md)
