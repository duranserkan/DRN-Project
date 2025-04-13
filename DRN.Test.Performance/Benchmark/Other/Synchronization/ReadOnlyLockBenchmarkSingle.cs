using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Xunit.Abstractions;

namespace DRN.Test.Performance.Benchmark.Other.Synchronization;

public class ReadOnlyLockBenchmarkSingleTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<ReadOnlyLockBenchmarkSingle>(config);

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

public class ReadOnlyLockBenchmarkSingle
{
    private readonly DateTimeOffset _initialTime = DateTimeOffset.UtcNow;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly ReaderWriterLockSlim _syncLock = new();
    private readonly object _oldLock = new();
    private readonly Lock _newLock = new();

    [GlobalCleanup]
    public void Cleanup() => _syncLock?.Dispose();


    [Benchmark(Baseline = true)]
    public DateTimeOffset Lockless() => _initialTime + _stopwatch.Elapsed;


    [Benchmark]
    public DateTimeOffset SimpleLock()
    {
        lock (_oldLock)
            return _initialTime + _stopwatch.Elapsed;
    }

    [Benchmark]
    public DateTimeOffset SimpleLockNew()
    {
        lock (_newLock)
            return _initialTime + _stopwatch.Elapsed;
    }

    [Benchmark]
    public DateTimeOffset ReaderWriterLockSlimTest()
    {
        _syncLock.EnterReadLock();
        var date = _initialTime + _stopwatch.Elapsed;
        _syncLock.ExitReadLock();

        return date;
    }
}