using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

public class SourceKnownIdGeneratorPerformanceTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<SourceKnownIdGeneratorBenchmark>(config);

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


public class SourceKnownIdGeneratorBenchmark
{
    private static SourceKnownIdUtils Utils { get; } = new(new AppSettings(new ConfigurationManager()));

    [GlobalSetup]
    public void Setup()
    {
    }

    [Benchmark]
    public long SourceKnownId() => Utils.Next<SourceKnownIdGeneratorBenchmark>();
}