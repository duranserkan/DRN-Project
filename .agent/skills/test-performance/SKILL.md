---
name: test-performance
description: Use when measuring or reviewing performance, benchmarks, load tests, stress tests, spike tests, performance regressions, or optimization claims.
last-updated: 2026-11-13
difficulty: intermediate
---

# Performance Testing

> Performance testing with the repository's benchmark and load-test tooling.

## When to Apply

- Benchmarking code performance.
- Load testing APIs.
- Measuring performance regressions.
- Validating optimization claims.

## BenchmarkDotNet

Discover the benchmark project and runner from the profile or manifests. Obtain explicit execution permission under `AGENTS.md`. A test permission does not automatically authorize benchmarks. Compare equivalent public behavior under representative inputs.

| Attribute | Purpose |
|-----------|---------|
| `[Benchmark(Baseline = true)]` | Baseline for comparison |
| `[MemoryDiagnoser]` | Memory allocation metrics |
| `[GlobalSetup]` / `[IterationSetup]` | Setup hooks |
| `[Params(1, 10, 100)]` | Parameterized benchmarks |

## K6 Load Testing

Discover existing load-test scripts from the profile or repository. Obtain explicit permission for the command, target environment, and load. Do not infer authorization to generate helpers or contact production.

| Test Type | Purpose | Config |
|-----------|---------|--------|
| Load | Normal load | `vus: 10, duration: '5m'` |
| Stress | Beyond capacity | Ramp up VUs until failure |
| Spike | Sudden load | Quick ramp to high VUs |
| Soak | Extended duration | Low VUs, long duration |

## Best Practices

- Use release mode for micro-benchmarks.
- Avoid parallel benchmark execution unless the framework supports it.
- Document baselines to track regressions.
- Tie optimization claims to before/after measurements.

## Related Skills

- [test-unit](../test-unit/SKILL.md)
- [test-integration](../test-integration/SKILL.md)
