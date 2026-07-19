```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.5.2 (25F84) [Darwin 25.5.0]
Apple M2, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a


```
| Method                                | Mean     | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|-------------------------------------- |---------:|---------:|---------:|------:|----------:|------------:|
| GetIntClaimFromIdentity               | 22.72 ns | 0.053 ns | 0.041 ns |  1.00 |         - |          NA |
| GetIntClaimFromScopedUserWithoutCache | 10.84 ns | 0.011 ns | 0.010 ns |  0.48 |         - |          NA |
| GetCachedIntClaim                     | 51.46 ns | 0.040 ns | 0.038 ns |  2.26 |         - |          NA |
