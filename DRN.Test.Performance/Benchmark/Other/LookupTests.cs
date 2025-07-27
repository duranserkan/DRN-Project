using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Xunit.Abstractions;

namespace DRN.Test.Performance.Benchmark.Other;

public class LookupTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<LookupBenchmark>(config);

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
public class LookupBenchmark
{
    public Dictionary<string, long> Dictionary1 { get; } = new();
    public Dictionary<string, long> Dictionary10 { get; set; } = new();
    public Dictionary<string, long> Dictionary100 { get; set; } = new();
    public Dictionary<string, long> Dictionary1_000 { get; set; } = new();
    public Dictionary<string, long> Dictionary10_000 { get; set; } = new();
    public Dictionary<string, long> Dictionary100_000 { get; set; } = new();
    public Dictionary<string, long> Dictionary1_000_000 { get; set; } = new();

    public HashSet<string> Set1 { get; set; } = new();
    public HashSet<string> Set10 { get; set; } = new();
    public HashSet<string> Set100 { get; set; } = new();
    public HashSet<string> Set1_000 { get; set; } = new();
    public HashSet<string> Set10_000 { get; set; } = new();
    public HashSet<string> Set100_000 { get; set; } = new();
    public HashSet<string> Set1_000_000 { get; set; } = new();

    [GlobalSetup]
    public void Setup()
    {
        Dictionary1["1"] = 1;
        Dictionary10 = Enumerable.Range(1, 11).ToDictionary(x => x.ToString(), x => (long)x);
        Dictionary100 = Enumerable.Range(1, 101).ToDictionary(x => x.ToString(), x => (long)x);
        Dictionary1_000 = Enumerable.Range(1, 1_001).ToDictionary(x => x.ToString(), x => (long)x);
        Dictionary10_000 = Enumerable.Range(1, 10_001).ToDictionary(x => x.ToString(), x => (long)x);
        Dictionary100_000 = Enumerable.Range(1, 100_001).ToDictionary(x => x.ToString(), x => (long)x);
        Dictionary1_000_000 = Enumerable.Range(1, 1_000_001).ToDictionary(x => x.ToString(), x => (long)x);

        Set1.Add("1");
        Set10 = Enumerable.Range(1, 11).Select(x => x.ToString()).ToHashSet();
        Set100 = Enumerable.Range(1, 101).Select(x => x.ToString()).ToHashSet();
        Set1_000 = Enumerable.Range(1, 1_001).Select(x => x.ToString()).ToHashSet();
        Set10_000 = Enumerable.Range(1, 10_001).Select(x => x.ToString()).ToHashSet();
        Set100_000 = Enumerable.Range(1, 100_001).Select(x => x.ToString()).ToHashSet();
        Set1_000_000 = Enumerable.Range(1, 1_000_001).Select(x => x.ToString()).ToHashSet();
    }

    [Benchmark]
    public long Dictionary1Lookup() => Dictionary1["1"];

    [Benchmark]
    public long Dictionary10Lookup() => Dictionary10["10"];

    [Benchmark]
    public long Dictionary100Lookup() => Dictionary100["100"];

    [Benchmark]
    public long Dictionary1_000Lookup() => Dictionary1_000["1000"];

    [Benchmark]
    public long Dictionary10_000Lookup() => Dictionary10_000["10000"];

    [Benchmark]
    public long Dictionary100_000Lookup() => Dictionary100_000["100000"];

    [Benchmark]
    public long Dictionary1_000_000Lookup() => Dictionary1_000_000["1000000"];
    
    [Benchmark]
    public bool Set1Lookup() => Set1.Contains("1");
    [Benchmark]
    public bool Set10Lookup() => Set10.Contains("10");
    [Benchmark]
    public bool Set100Lookup() => Set100.Contains("100");
    [Benchmark]
    public bool Set1_000Lookup() => Set1_000.Contains("1000");
    [Benchmark]
    public bool Set10_000Lookup() => Set10_000.Contains("10000");
    [Benchmark]
    public bool Set100_000Lookup() => Set100_000.Contains("100000");
    [Benchmark]
    public bool Set1_000_000Lookup() => Set1_000_000.Contains("1000000");

}