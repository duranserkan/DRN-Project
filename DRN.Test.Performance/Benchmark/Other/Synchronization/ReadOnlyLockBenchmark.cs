using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
namespace DRN.Test.Performance.Benchmark.Other.Synchronization;

[MemoryDiagnoser]
public class ReadOnlyLockBenchmarkTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<ReadOnlyLockBenchmark>(config);

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

public class ReadOnlyLockBenchmark
{
    private readonly DateTimeOffset _initialTime = DateTimeOffset.UtcNow;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly ReaderWriterLockSlim _syncLock = new();
    private readonly object _oldLock = new();
    private readonly Lock _newLock = new();

    [GlobalCleanup]
    public void Cleanup() => _syncLock?.Dispose();

    [Params(1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024)]
    public int ThreadCount { get; set; }
    
    [Benchmark(Baseline = true)]
    public DateTimeOffset[] Lockless() => Enumerable.Range(1, ThreadCount)
        .AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(_ => _initialTime + _stopwatch.Elapsed).ToArray();


    [Benchmark]
    public DateTimeOffset[] SimpleLock() => Enumerable.Range(1, ThreadCount)
        .AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(_ =>
        {
            lock (_oldLock)
                return _initialTime + _stopwatch.Elapsed;
        }).ToArray();

    [Benchmark]
    public DateTimeOffset[] SimpleLockNew() => Enumerable.Range(1, ThreadCount)
        .AsParallel().WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .WithDegreeOfParallelism(8)
        .Select(_ =>
        {
            lock (_newLock)
                return _initialTime + _stopwatch.Elapsed;
        }).ToArray();

    [Benchmark]
    public DateTimeOffset[] ReaderWriterLockSlimTest() => Enumerable.Range(1, ThreadCount)
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(_ =>
        {
            _syncLock.EnterReadLock();
            var date = _initialTime + _stopwatch.Elapsed;
            _syncLock.ExitReadLock();

            return date;
        }).ToArray();
}