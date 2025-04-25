using System.Buffers.Binary;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;
using Perfolizer.Mathematics.OutlierDetection;
using Xunit.Abstractions;

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

public class SourceKnownIdUtilsPerformanceTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<SourceKnownIdUtilsBenchmark>(config);

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

[Outliers(OutlierMode.RemoveUpper)]
public class SourceKnownIdUtilsBenchmark
{
    static SourceKnownIdUtilsBenchmark()
    {
        Utils = new(AppSettings.Development());
        EntityIdUtils = new(AppSettings.Development(), Utils);
    }

    private static SourceKnownIdUtils Utils { get; }
    private static SourceKnownEntityIdUtils EntityIdUtils { get; }
    private static YEntity Entity { get; } = new(5);

    [Benchmark]
    public long RandomLong() => BinaryPrimitives.ReadInt64LittleEndian(RandomNumberGenerator.GetBytes(8));

    [Benchmark]
    public Guid RandomGuidV4() => Guid.NewGuid();

    [Benchmark]
    public Guid RandomGuidV7() => Guid.CreateVersion7();
    
    [Benchmark]
    public DateTimeOffset MonotonicSystemDateTime_UtcNow() => MonotonicSystemDateTime.UtcNow;
    
    [Benchmark]
    public long TimeStampManager_TimeStamp() => TimeStampManager.CurrentTimestamp(SourceKnownIdUtils.DefaultEpoch);
    
    [Benchmark] //todo TimeScopedId look like a bottleneck, review it for possible improvements
    public SequenceTimeScopedId SequenceManager_TimeScopedId() => SequenceManager<YEntity>.GetTimeScopedId();

    [Benchmark]
    public long SourceKnownId() => Utils.Next<SourceKnownIdUtilsBenchmark>();

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityId()
    {
        return EntityIdUtils.Generate(new YEntity(Utils.Next<SourceKnownIdUtilsBenchmark>()));
    }
    
    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdWithProvidedLongValue()
    {
        return EntityIdUtils.Generate(Entity);
    }
}

[EntityTypeId(92)]
public class YEntity(long id) : Entity(id);