```

BenchmarkDotNet v0.15.8, macOS Tahoe 26.5.2 (25F84) [Darwin 25.5.0]
Apple M2, 1 CPU, 8 logical and 8 physical cores
.NET SDK 10.0.302
  [Host]     : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a
  Job-TMIHRW : .NET 10.0.10 (10.0.10, 10.0.1026.32716), Arm64 RyuJIT armv8.0-a

OutlierMode=RemoveUpper  InvocationCount=2097152  IterationCount=30  
UnrollFactor=1  WarmupCount=1  

```
| Method                          | Size | Mean       | Error     | StdDev    | Median     | Gen0   | Allocated |
|-------------------------------- |----- |-----------:|----------:|----------:|-----------:|-------:|----------:|
| **Blake3_256_Update**               | **8**    | **117.676 ns** | **0.2127 ns** | **0.3050 ns** | **117.608 ns** |      **-** |         **-** |
| Blake3_256_UpdateWithJoin       | 8    | 117.840 ns | 0.1099 ns | 0.1646 ns | 117.839 ns |      - |         - |
| Blake3_256_New_Update           | 8    | 201.356 ns | 0.6967 ns | 1.0427 ns | 201.333 ns | 0.2427 |    2032 B |
| Blake3_256_New_Update_With_Join | 8    | 205.890 ns | 0.6312 ns | 0.9252 ns | 205.797 ns | 0.2427 |    2032 B |
| Hmac_Sha256                     | 8    | 311.819 ns | 0.8948 ns | 1.3116 ns | 312.028 ns |      - |         - |
| Hmac_Sha512                     | 8    | 731.630 ns | 0.5436 ns | 0.7795 ns | 731.591 ns |      - |         - |
| Fast_Crc32                      | 8    |   1.138 ns | 0.0283 ns | 0.0378 ns |   1.124 ns |      - |         - |
| Fast_Crc64                      | 8    |   4.972 ns | 0.0617 ns | 0.0885 ns |   4.921 ns |      - |         - |
| Fast_XxHash3                    | 8    |   1.435 ns | 0.0313 ns | 0.0418 ns |   1.420 ns |      - |         - |
| Fast_XxHash32                   | 8    |   2.787 ns | 0.0507 ns | 0.0676 ns |   2.760 ns |      - |         - |
| Fast_XxHash64                   | 8    |   4.201 ns | 0.0578 ns | 0.0828 ns |   4.162 ns |      - |         - |
| Fast_XxHash128                  | 8    |   2.012 ns | 0.0095 ns | 0.0123 ns |   2.011 ns |      - |         - |
| **Blake3_256_Update**               | **12**   | **114.442 ns** | **0.0834 ns** | **0.1196 ns** | **114.441 ns** |      **-** |         **-** |
| Blake3_256_UpdateWithJoin       | 12   | 117.008 ns | 0.0919 ns | 0.1317 ns | 117.003 ns |      - |         - |
| Blake3_256_New_Update           | 12   | 201.451 ns | 0.6066 ns | 0.9079 ns | 201.403 ns | 0.2427 |    2032 B |
| Blake3_256_New_Update_With_Join | 12   | 205.370 ns | 0.7196 ns | 1.0547 ns | 205.459 ns | 0.2427 |    2032 B |
| Hmac_Sha256                     | 12   | 303.102 ns | 0.6203 ns | 0.9285 ns | 303.087 ns |      - |         - |
| Hmac_Sha512                     | 12   | 725.186 ns | 0.2987 ns | 0.4089 ns | 725.190 ns |      - |         - |
| Fast_Crc32                      | 12   |   2.305 ns | 0.0168 ns | 0.0218 ns |   2.302 ns |      - |         - |
| Fast_Crc64                      | 12   |   8.490 ns | 0.0723 ns | 0.1083 ns |   8.445 ns |      - |         - |
| Fast_XxHash3                    | 12   |   1.564 ns | 0.0095 ns | 0.0124 ns |   1.561 ns |      - |         - |
| Fast_XxHash32                   | 12   |   3.381 ns | 0.0517 ns | 0.0742 ns |   3.352 ns |      - |         - |
| Fast_XxHash64                   | 12   |   5.016 ns | 0.0560 ns | 0.0767 ns |   4.987 ns |      - |         - |
| Fast_XxHash128                  | 12   |   2.391 ns | 0.0137 ns | 0.0179 ns |   2.392 ns |      - |         - |
| **Blake3_256_Update**               | **16**   | **114.682 ns** | **0.1108 ns** | **0.1624 ns** | **114.697 ns** |      **-** |         **-** |
| Blake3_256_UpdateWithJoin       | 16   | 116.917 ns | 0.0906 ns | 0.1240 ns | 116.933 ns |      - |         - |
| Blake3_256_New_Update           | 16   | 201.975 ns | 0.5676 ns | 0.8496 ns | 201.853 ns | 0.2427 |    2032 B |
| Blake3_256_New_Update_With_Join | 16   | 205.148 ns | 0.7526 ns | 1.1265 ns | 205.166 ns | 0.2427 |    2032 B |
| Hmac_Sha256                     | 16   | 296.278 ns | 0.6280 ns | 0.9400 ns | 296.235 ns |      - |         - |
| Hmac_Sha512                     | 16   | 717.004 ns | 0.2526 ns | 0.3372 ns | 716.974 ns |      - |         - |
| Fast_Crc32                      | 16   |   1.478 ns | 0.0149 ns | 0.0194 ns |   1.476 ns |      - |         - |
| Fast_Crc64                      | 16   |   2.303 ns | 0.0169 ns | 0.0220 ns |   2.300 ns |      - |         - |
| Fast_XxHash3                    | 16   |   1.708 ns | 0.0102 ns | 0.0130 ns |   1.708 ns |      - |         - |
| Fast_XxHash32                   | 16   |   2.631 ns | 0.0255 ns | 0.0340 ns |   2.620 ns |      - |         - |
| Fast_XxHash64                   | 16   |   5.329 ns | 0.0637 ns | 0.0893 ns |   5.287 ns |      - |         - |
| Fast_XxHash128                  | 16   |   2.609 ns | 0.0188 ns | 0.0244 ns |   2.604 ns |      - |         - |
