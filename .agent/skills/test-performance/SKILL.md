---
name: test-performance
description: Performance testing and benchmarking - BenchmarkDotNet for micro-benchmarks, K6 for load/stress/spike testing, performance regression tracking, and report generation. Use for measuring and optimizing performance. Keywords: performance-testing, benchmarking, benchmarkdotnet, k6, load-testing, stress-testing, performance-optimization, regression-testing, dtt
last-updated: 2026-02-15
difficulty: intermediate
---

# DRN.Test.Performance

> Performance testing with BenchmarkDotNet and K6 load testing.

## When to Apply
- Benchmarking code performance
- Load testing APIs
- Measuring performance regressions

---

## BenchmarkDotNet

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
dotnet run -c Release --project DRN.Test.Performance                       # All benchmarks
dotnet run -c Release --project DRN.Test.Performance -- --filter "*Name*"  # Filtered
```

| Attribute | Purpose |
|-----------|---------|
| `[Benchmark(Baseline = true)]` | Baseline for comparison |
| `[MemoryDiagnoser]` | Memory allocation metrics |
| `[GlobalSetup]` / `[IterationSetup]` | Setup hooks |
| `[Params(1, 10, 100)]` | Parameterized benchmarks |

---

## K6 Load Testing

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 10, duration: '30s',
    thresholds: {
        http_req_failed: ['rate<0.01'],
        http_req_duration: ['p(95)<500'],
    },
};

export default function () {
    const res = http.get('http://localhost:5988/api/status');
    check(res, {
        'is status 200': (r) => r.status === 200,
        'response time < 200ms': (r) => r.timings.duration < 200,
    });
    sleep(1);
}
```

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

---

## Best Practices

- **Release mode only** for BenchmarkDotNet
- **No parallel execution** (`parallelizeTestCollections: false`)
- **Document baselines** to track regressions
- Reports generated in `Reports/BenchmarkDotNet.Artifacts/` (HTML, MD, JSON)

---

## Related Skills

- [overview-drn-testing.md](../overview-drn-testing/SKILL.md) - Testing philosophy
- [drn-testing.md](../drn-testing/SKILL.md) - Framework.Testing
