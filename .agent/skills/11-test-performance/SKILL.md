---
name: test-performance
description: DRN.Test.Performance - BenchmarkDotNet usage, K6 load testing, and performance reports
---

# DRN.Test.Performance

> Performance testing with BenchmarkDotNet and K6 load testing.

## When to Apply
- Benchmarking code performance
- Load testing APIs
- Measuring performance regressions
- Generating performance reports

---

## Project Structure

```
DRN.Test.Performance/
├── Benchmark/          # BenchmarkDotNet benchmarks
│   ├── Framework/      # Framework benchmarks
│   └── Sample/         # Application benchmarks
├── K6/                 # K6 load test scripts
│   ├── scripts/        # Test scripts
│   └── config/         # K6 configuration
├── Reports/            # Generated reports
│   ├── BenchmarkDotNet.Artifacts/
│   └── K6Results/
├── Sketch.cs           # Experimental benchmarks
├── Todo.cs             # Planned benchmarks
└── xunit.runner.json
```

---

## BenchmarkDotNet

### Basic Benchmark

```csharp
[MemoryDiagnoser]
public class MyBenchmark
{
    private readonly List<int> _data = Enumerable.Range(0, 1000).ToList();
    
    [Benchmark(Baseline = true)]
    public int ForLoop()
    {
        var sum = 0;
        for (var i = 0; i < _data.Count; i++)
            sum += _data[i];
        return sum;
    }
    
    [Benchmark]
    public int LinqSum() => _data.Sum();
    
    [Benchmark]
    public int Foreach()
    {
        var sum = 0;
        foreach (var item in _data)
            sum += item;
        return sum;
    }
}
```

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release --project DRN.Test.Performance

# Run specific benchmark
dotnet run -c Release --project DRN.Test.Performance -- --filter "*MyBenchmark*"
```

### Benchmark Attributes

| Attribute | Purpose |
|-----------|---------|
| `[Benchmark]` | Mark method as benchmark |
| `[Benchmark(Baseline = true)]` | Set baseline for comparison |
| `[MemoryDiagnoser]` | Include memory allocation metrics |
| `[GlobalSetup]` | One-time setup before all iterations |
| `[IterationSetup]` | Setup before each iteration |
| `[Params(1, 10, 100)]` | Parameterized benchmarks |

---

## K6 Load Testing

### Basic K6 Script

```javascript
// k6/scripts/api-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 10,           // Virtual users
    duration: '30s',   // Test duration
    thresholds: {
        http_req_failed: ['rate<0.01'],       // <1% failures
        http_req_duration: ['p(95)<500'],     // 95th percentile under 500ms
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

### Running K6

```bash
# Run load test
k6 run k6/scripts/api-load-test.js

# Run with custom VUs and duration
k6 run --vus 50 --duration 1m k6/scripts/api-load-test.js

# Output to JSON
k6 run --out json=results.json k6/scripts/api-load-test.js
```

### K6 Test Types

| Type | Purpose | Configuration |
|------|---------|---------------|
| **Load Test** | Normal load | `vus: 10, duration: '5m'` |
| **Stress Test** | Beyond capacity | Ramp up VUs until failure |
| **Spike Test** | Sudden load | Quick ramp to high VUs |
| **Soak Test** | Extended duration | Low VUs, long duration |

---

## Reports

### BenchmarkDotNet Output

Reports generated in `Reports/BenchmarkDotNet.Artifacts/`:
- HTML reports
- Markdown reports
- JSON results

### K6 Output

```bash
# HTML report (with k6-reporter)
k6 run --out json=report.json script.js

# Grafana + InfluxDB (real-time)
k6 run --out influxdb=http://localhost:8086/k6 script.js
```

---

## xunit.runner.json

```json
{
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false
}
```

Performance tests should not run in parallel.

---

## Best Practices

| Practice | Reason |
|----------|--------|
| Run in Release mode | Accurate performance numbers |
| Warm-up iterations | JIT compilation effects |
| Multiple iterations | Statistical significance |
| Isolate tests | No parallel execution |
| Document baseline | Track regressions |

---

## Related Skills

- [03-drn-testing-overview.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/03-drn-testing-overview.md) - Testing philosophy
- [08-testing.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/08-testing.md) - Framework.Testing

---
