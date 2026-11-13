---
name: overview-drn-testing
description: "Choose unit, integration, API or performance tests with Duran's Testing Technique (DTT)."
last-updated: 2026-11-13
difficulty: basic
---

# DRN Testing Overview

DTT favors honest feedback with the least necessary test machinery. [drn-testing](../drn-testing/SKILL.md) owns data attributes and contexts.

## DTT Principles

| Principle | Practice |
|---|---|
| Security first | Avoid leaking secrets in fixtures, logs, snapshots, and generated data |
| Correctness over speed | Prefer real dependencies when mocks would hide behavior |
| Clarity over cleverness | One readable flow beats many tiny duplicate tests |
| Simplicity | Use `[Fact]` when no data, generation, or context is needed |
| Performance with proof | Benchmark only when a performance question is explicit |

## Test Type Selection

| Change | Default test type | Why |
|---|---|---|
| Pure utility, domain invariant, deterministic logic | Unit | Use `DataInlineUnit` when data/generation helps |
| Attribute DI registration or isolated service behavior | Unit | `DrnTestContextUnit` validates wiring without Docker |
| Repository, EF mapping, query, transaction, concurrency | Integration DB | Real PostgreSQL is the useful signal |
| Controller, endpoint, auth, middleware, serialization | API integration | Full request pipeline via `ApplicationContext` |
| External HTTP client behavior | Integration/API | Use `FlurlHttpTest` against outbound URL patterns |
| Throughput, allocation, latency, load | Performance | BenchmarkDotNet/K6 only with explicit user intent |

## Project Shape

Resolve project names and paths from the current repository profile or filesystem. These are test roles, not required folder names:

| Role | Coverage |
|---|---|
| Unit | Isolated logic and service validation |
| Analyzer, when present | Roslyn analyzer regression tests |
| Integration | Testcontainers, repositories, API/E2E |
| Performance, when present | BenchmarkDotNet and K6 |

## Routing

- Need exact attribute/context rules or MTP commands: [drn-testing](../drn-testing/SKILL.md)
- Writing isolated tests: [test-unit](../test-unit/SKILL.md)
- Choosing API vs DB integration: [test-integration](../test-integration/SKILL.md)
- Endpoint behavior: [test-integration-api](../test-integration-api/SKILL.md)
- Repository/database behavior: [test-integration-db](../test-integration-db/SKILL.md)
- Performance work: [test-performance](../test-performance/SKILL.md)
