```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.5.2 (25F84) [Darwin 25.5.0]
Apple M2, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
  Job-TMIHRW : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

OutlierMode=RemoveUpper  InvocationCount=2097152  IterationCount=30  
UnrollFactor=1  WarmupCount=1  

```
| Method                          | Size | Mean       | Error     | StdDev    | Allocated |
|-------------------------------- |----- |-----------:|----------:|----------:|----------:|
| **Blake3_256_Update**               | **8**    |  **80.692 ns** | **0.0660 ns** | **0.0881 ns** |         **-** |
| Blake3_256_UpdateWithJoin       | 8    |  82.086 ns | 0.1013 ns | 0.1453 ns |         - |
| Blake3_256_New_Update           | 8    |  95.336 ns | 0.0994 ns | 0.1458 ns |         - |
| Blake3_256_New_Update_With_Join | 8    | 104.368 ns | 0.0910 ns | 0.1305 ns |         - |
| Hmac_Sha256                     | 8    | 313.291 ns | 1.2024 ns | 1.7997 ns |         - |
| Hmac_Sha512                     | 8    | 731.784 ns | 0.6927 ns | 1.0368 ns |         - |
| Fast_Crc32                      | 8    |   1.126 ns | 0.0120 ns | 0.0152 ns |         - |
| Fast_Crc64                      | 8    |   4.954 ns | 0.0690 ns | 0.0990 ns |         - |
| Fast_XxHash3                    | 8    |   1.424 ns | 0.0193 ns | 0.0251 ns |         - |
| Fast_XxHash32                   | 8    |   2.770 ns | 0.0258 ns | 0.0335 ns |         - |
| Fast_XxHash64                   | 8    |   4.208 ns | 0.0657 ns | 0.0942 ns |         - |
| Fast_XxHash128                  | 8    |   2.009 ns | 0.0108 ns | 0.0140 ns |         - |
| **Blake3_256_Update**               | **12**   |  **81.873 ns** | **0.0823 ns** | **0.1180 ns** |         **-** |
| Blake3_256_UpdateWithJoin       | 12   |  82.914 ns | 0.0795 ns | 0.1115 ns |         - |
| Blake3_256_New_Update           | 12   |  95.808 ns | 0.0879 ns | 0.1316 ns |         - |
| Blake3_256_New_Update_With_Join | 12   |  99.643 ns | 0.0927 ns | 0.1358 ns |         - |
| Hmac_Sha256                     | 12   | 301.022 ns | 0.3943 ns | 0.5528 ns |         - |
| Hmac_Sha512                     | 12   | 727.085 ns | 0.4312 ns | 0.5756 ns |         - |
| Fast_Crc32                      | 12   |   2.306 ns | 0.0222 ns | 0.0296 ns |         - |
| Fast_Crc64                      | 12   |   8.454 ns | 0.0784 ns | 0.1174 ns |         - |
| Fast_XxHash3                    | 12   |   1.578 ns | 0.0269 ns | 0.0368 ns |         - |
| Fast_XxHash32                   | 12   |   3.387 ns | 0.0524 ns | 0.0735 ns |         - |
| Fast_XxHash64                   | 12   |   5.034 ns | 0.0607 ns | 0.0851 ns |         - |
| Fast_XxHash128                  | 12   |   2.403 ns | 0.0229 ns | 0.0297 ns |         - |
| **Blake3_256_Update**               | **16**   |  **81.138 ns** | **0.1369 ns** | **0.1919 ns** |         **-** |
| Blake3_256_UpdateWithJoin       | 16   |  82.350 ns | 0.0943 ns | 0.1321 ns |         - |
| Blake3_256_New_Update           | 16   | 106.509 ns | 0.1105 ns | 0.1585 ns |         - |
| Blake3_256_New_Update_With_Join | 16   |  96.530 ns | 0.0889 ns | 0.1275 ns |         - |
| Hmac_Sha256                     | 16   | 296.060 ns | 0.6343 ns | 0.9494 ns |         - |
| Hmac_Sha512                     | 16   | 718.300 ns | 0.4482 ns | 0.5983 ns |         - |
| Fast_Crc32                      | 16   |   1.483 ns | 0.0149 ns | 0.0194 ns |         - |
| Fast_Crc64                      | 16   |   2.312 ns | 0.0167 ns | 0.0217 ns |         - |
| Fast_XxHash3                    | 16   |   1.727 ns | 0.0307 ns | 0.0400 ns |         - |
| Fast_XxHash32                   | 16   |   2.637 ns | 0.0274 ns | 0.0365 ns |         - |
| Fast_XxHash64                   | 16   |   5.339 ns | 0.0772 ns | 0.1082 ns |         - |
| Fast_XxHash128                  | 16   |   2.612 ns | 0.0236 ns | 0.0315 ns |         - |
