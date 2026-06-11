---
name: test-performance
description: Use when measuring or reviewing performance, benchmarks, load tests, stress tests, spike tests, performance regressions, or optimization claims.
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~0.5K
---

# Performance Testing

> Performance testing with the repository's benchmark and load-test tooling.

## When to Apply

- Benchmarking code performance.
- Load testing APIs.
- Measuring performance regressions.
- Validating optimization claims.

## BenchmarkDotNet

Run benchmark commands only when the user explicitly allows build/test execution. Use the repository profile command when available; otherwise discover the benchmark project and set `PERFORMANCE_PROJECT`.

```csharp
[MemoryDiagnoser]
public class MyBenchmark
{
    private readonly List<int> _data = Enumerable.Range(0, 1000).ToList();

    [Benchmark(Baseline = true)]
    public int ForLoop()
    {
        var sum = 0;
        for (var i = 0; i < _data.Count; i++) sum += _data[i];
        return sum;
    }

    [Benchmark]
    public int LinqSum() => _data.Sum();
}
```

```bash
dotnet run -c Release --project "$PERFORMANCE_PROJECT"
dotnet run -c Release --project "$PERFORMANCE_PROJECT" -- --filter-method Fully.Qualified.PerformanceTestClass.Run_Benchmarks
```

| Attribute | Purpose |
|-----------|---------|
| `[Benchmark(Baseline = true)]` | Baseline for comparison |
| `[MemoryDiagnoser]` | Memory allocation metrics |
| `[GlobalSetup]` / `[IterationSetup]` | Setup hooks |
| `[Params(1, 10, 100)]` | Parameterized benchmarks |

## K6 Load Testing

Run load-test commands only when the user explicitly allows load-test execution. Discover script paths from the repository profile or the `k6/` folder.

```bash
k6 run k6/scripts/api-load-test.js
k6 run --vus 50 --duration 1m k6/scripts/api-load-test.js
k6 run --out json=results.json k6/scripts/api-load-test.js
```

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
