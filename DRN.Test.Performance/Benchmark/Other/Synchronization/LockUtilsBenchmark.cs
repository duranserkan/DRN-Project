using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Concurrency;
using Perfolizer.Mathematics.OutlierDetection;

namespace DRN.Test.Performance.Benchmark.Other.Synchronization;

public class LockUtilsBenchmarkTests(ITestOutputHelper output)
{
#if !DEBUG
    [Fact] //should run on release build
#endif
    public void Run_Benchmarks()
    {
        var logger = new AccumulationLogger();
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddLogger(logger)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
        var summary = BenchmarkRunner.Run<LockUtilsBenchmark>(config);
        summary.Reports.Should().NotBeEmpty();

        output.WriteLine("===================================");
        output.WriteLine("Benchmark Results Path");
        output.WriteLine("===================================");
        output.WriteLine(summary.ResultsDirectoryPath);
        output.WriteLine("===================================");
        output.WriteLine("Benchmark Logs");
        output.WriteLine("===================================");

        var log = logger.GetLog();
        var lines = log.Split(Environment.NewLine);
        foreach (var line in lines)
            output.WriteLine(line);
    }
}

/// <summary>
/// Compares equivalent counter updates from an uncontended single worker (direct sequential loop without Parallel.For)
/// and 8 contended workers (via Parallel.For) performing 10 operations per worker.
/// Compare ratios within each category ("WorkerCount1_Ops10" and "WorkerCount8_Ops10").
/// </summary>
[Outliers(OutlierMode.RemoveUpper)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LockUtilsBenchmark
{
    private int OperationsPerWorker { get; set; } = 10;
    private const int MultiWorkerCount = 8;

    private readonly Lock _systemLock = new();
    private int _scopeLockValue;

    private readonly ParallelOptions _parallelOptions8 = new() { MaxDegreeOfParallelism = MultiWorkerCount };

    // --- Worker Count 1 (Isolated - No Parallel.For Overhead) ---
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("WorkerCount1_Ops10")]
    public long WorkerCount1_SystemThreadingLock()
    {
        var counter = 0L;

        for (var operation = 0; operation < OperationsPerWorker; operation++)
            lock (_systemLock)
                counter++;

        return counter;
    }

    [Benchmark]
    [BenchmarkCategory("WorkerCount1_Ops10")]
    public long WorkerCount1_LockUtilsClaimScope()
    {
        _scopeLockValue = 0;
        var counter = 0L;

        for (var operation = 0; operation < OperationsPerWorker; operation++)
        {
            LockUtils.LockScope scope;
            while (!(scope = LockUtils.TryClaimScope(ref _scopeLockValue)).Acquired)
            {
            }

            using (scope)
                counter++;
        }

        return counter;
    }

    // --- Worker Count 8 (Contended - Parallel.For) ---
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("WorkerCount8_Ops10")]
    public long WorkerCount8_SystemThreadingLock()
    {
        var counter = 0L;

        Parallel.For(0, MultiWorkerCount, _parallelOptions8, _ =>
        {
            for (var operation = 0; operation < OperationsPerWorker; operation++)
                lock (_systemLock)
                    counter++;
        });

        return counter;
    }

    [Benchmark]
    [BenchmarkCategory("WorkerCount8_Ops10")]
    public long WorkerCount8_LockUtilsClaimScope()
    {
        _scopeLockValue = 0;
        var counter = 0L;

        Parallel.For(0, MultiWorkerCount, _parallelOptions8, _ =>
        {
            for (var operation = 0; operation < OperationsPerWorker; operation++)
            {
                LockUtils.LockScope scope;
                while (!(scope = LockUtils.TryClaimScope(ref _scopeLockValue)).Acquired)
                {
                }

                using (scope)
                    counter++;
            }
        });

        return counter;
    }
}
