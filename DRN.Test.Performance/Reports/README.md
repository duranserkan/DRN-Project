# Performance Reports

This directory contains retained BenchmarkDotNet results, organized by
benchmark and run. Report files from the same run stay together.

## Groups

| Path | Contents |
|---|---|
| `Auth/ScopedUser/<run>/` | `ScopedUserBenchmark` report sets |
| `Encryption/Aes256/<run>/` | `Aes256Benchmark` report sets |
| `SourceKnownIdUtils/Standard/<run>/` | Standard `SourceKnownIdUtilsBenchmark` history |
| `SourceKnownIdUtils/Saturation/<run>/` | Saturation benchmark history |
| `DateTimeProvider/` | `DateTimeProviderBenchmark` reports |
| `MethodUtils/` | `MethodUtilsBenchmark` reports |
| `Hash/General/` | General hash benchmark reports |
| `Hash/SmallPayload/<run>/` | Small-payload reports and implementation variants |
| `Lookup/` | `LookupBenchmark` reports |
| `Synchronization/LockUtils/` | `LockUtilsBenchmark` reports |
| `Synchronization/ReadOnlyLock/` | Read-only lock benchmark reports |

## Conventions

- Group reports by benchmark and then run.
- Name run directories `<YYYY-MM-DD>[_<runtime>]`.
- Place implementation variants such as `Managed` and `Native` below their
  shared run directory.
- Keep all formats from one run in the same directory.
- For curated report names, use
  `<Benchmark>-<YYYYMMDD>[-<runtime>][-<variant>].<extension>`.
- BenchmarkDotNet-generated `*-report.*` names may be retained when the complete
  report set is kept together.
- Historical standalone reports may remain directly in their benchmark
  directory when introducing a run directory would not improve grouping.
- Do not compare runs as controlled before/after evidence unless their runtime,
  BenchmarkDotNet version, operating system, hardware, and benchmark settings
  are equivalent.

Historical filenames are preserved to avoid implying metadata that was not
captured when the reports were created.
