using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Time;
using Xunit.Abstractions;

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

public class DateTimeProviderPerformanceTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<DateTimeProviderBenchmark>(config);

        output.WriteLine("===================================");
        output.WriteLine("Benchmark Results Path");
        output.WriteLine("===================================");
        output.WriteLine(summary.ResultsDirectoryPath);
        output.WriteLine("===================================");

        output.WriteLine("===================================");
        output.WriteLine("Benchmark Logs");
        output.WriteLine("===================================");
        var log = logger.GetLog();
        var lines = log.Split(Environment.NewLine);
        foreach (var line in lines)
            output.WriteLine(line);
        output.WriteLine("===================================");
    }
}

[MemoryDiagnoser]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net90)]
public class DateTimeProviderBenchmark
{
    private long _incrementCounter;
    private long _compareExchangeCounterSimple;
    private long _compareExchangeCounterFull;
    private Stopwatch _stopwatch = new();
    
    [GlobalSetup]
    public void Setup()
    {
        _incrementCounter = 0;
        _compareExchangeCounterSimple = 0;
        _compareExchangeCounterFull = 0;
        _stopwatch = Stopwatch.StartNew();

        // Warm up JIT for all operations
        for (var i = 0; i < 1000; i++)
        {
            _ = DateTime.UtcNow;
            _ = DateTimeOffset.UtcNow;
            _ = MonotonicSystemDateTime.UtcNow;
            _ = _stopwatch.Elapsed;
            _ = Stopwatch.GetTimestamp();
        }
    }

    [Benchmark]
    public TimeSpan Stopwatch_Elapsed() => _stopwatch.Elapsed;

    [Benchmark]
    public long Stopwatch_Tick() => _stopwatch.Elapsed.Ticks;

    [Benchmark]
    public long Stopwatch_Tick_Separate()
    {
        var time = _stopwatch.Elapsed;

        return time.Ticks;
    }

    [Benchmark]
    public DateTimeOffset Stopwatch_DateTime() => new(_stopwatch.Elapsed.Ticks + 2, TimeSpan.Zero);

    [Benchmark]
    public DateTimeOffset MonotonicSystemDateTime_UtcNow() => MonotonicSystemDateTime.UtcNow;

    [Benchmark]
    public DateTimeOffset SystemDateTime_UtcNow() => DateTimeOffset.UtcNow;

    [Benchmark(Baseline = true)]
    public long InterlockedIncrement() => Interlocked.Increment(ref _incrementCounter);

    [Benchmark]
    public long InterlockedCompareExchange_Simple()
    {
        var current = _compareExchangeCounterSimple;
        var newValue = current + 1;

        Interlocked.CompareExchange(ref _compareExchangeCounterSimple, newValue, current);

        return newValue;
    }

    [Benchmark]
    public long InterlockedCompareExchange_Full()
    {
        long current, newValue;
        do
        {
            current = _compareExchangeCounterFull;
            newValue = current + 1;
        } while (Interlocked.CompareExchange(ref _compareExchangeCounterFull, newValue, current) != current);

        return newValue;
    }
}