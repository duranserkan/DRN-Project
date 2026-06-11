---
name: overview-drn-testing
description: "DTT (Duran's Testing Technique) philosophy - choosing unit, integration, API, or performance tests and routing to canonical DRN.Framework.Testing guidance. Keywords: testing-philosophy, dtt, test-strategy, unit-testing, integration-testing, performance-testing"
last-updated: 2026-06-12
difficulty: basic
tokens: ~0.7K
---

# DRN Testing Overview

> DTT favors honest feedback with the least necessary test machinery. The canonical attribute/context matrix lives in [drn-testing](../drn-testing/SKILL.md).

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
| Pure utility, domain invariant, deterministic branch logic | Unit | No container value; use `DataInlineUnit` when data/generation helps |
| Attribute DI registration or isolated service behavior | Unit | `DrnTestContextUnit` validates wiring without Docker |
| Repository, EF mapping, query, transaction, concurrency | Integration DB | Real PostgreSQL is the useful signal |
| Controller, endpoint, auth, middleware, serialization | API integration | Full request pipeline via `ApplicationContext` |
| External HTTP client behavior | Integration/API | Use `FlurlHttpTest` against outbound URL patterns |
| Throughput, allocation, latency, load | Performance | BenchmarkDotNet/K6 only with explicit user intent |

## Project Shape

```text
DRN.Test.Unit/         # isolated logic and service validation
DRN.Test.Integration/  # Testcontainers, repositories, API/E2E
DRN.Test.Performance/  # BenchmarkDotNet and K6
```

Context names:

- `DrnTestContextUnit`: unit context, no containers
- `DrnTestContext`: integration context with `ContainerContext`, `ApplicationContext`, and `FlurlHttpTest`
- `ApplicationContext`: WebApplicationFactory wrapper for app/API tests

## Routing

- Need exact attribute/context rules or MTP commands: [drn-testing](../drn-testing/SKILL.md)
- Writing isolated tests: [test-unit](../test-unit/SKILL.md)
- Choosing API vs DB integration: [test-integration](../test-integration/SKILL.md)
- Endpoint behavior: [test-integration-api](../test-integration-api/SKILL.md)
- Repository/database behavior: [test-integration-db](../test-integration-db/SKILL.md)
- Performance work: [test-performance](../test-performance/SKILL.md)
