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

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

public class SourceKnownIdUtilsSaturationPerformanceTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<SourceKnownIdUtilsSaturationBenchmark>(config);

        output.WriteLine("===================================");
        output.WriteLine("Saturation Benchmark Results Path");
        output.WriteLine("===================================");
        output.WriteLine(summary.ResultsDirectoryPath);
        output.WriteLine("===================================");
        output.WriteLine("Saturation Benchmark Logs");
        output.WriteLine("===================================");

        var log = logger.GetLog();
        var lines = log.Split(Environment.NewLine);
        foreach (var line in lines)
            output.WriteLine(line);
    }
}

[MemoryDiagnoser]
[WarmupCount(1)]
[IterationCount(30)]
[InvocationCount(786_432)] // 3× sequence cap (2^18 = 262,144) — guarantees backpressure per iteration
public class SourceKnownIdUtilsSaturationBenchmark
{
    [IterationSetup]
    public void IterationWait() => Thread.Sleep(TimeStampManager.PrecisionUnitInMsSafeDelay); // Let SequenceTimeScope reset between iterations (one tick)
    static SourceKnownIdUtilsSaturationBenchmark()
    {
        Utils = new(AppSettings.Development(), new EpochTimeUtils());

        var appSettings = AppSettings.Development();
        SecureEntityIdUtils = new(appSettings, Utils);
        PlainEntityIdUtils = new(appSettings, Utils);

        // Pre-generate IDs for Parse and ToSecure/ToPlain benchmarks — avoids measuring ID generation
        var id = Utils.Next<SourceKnownIdUtilsSaturationBenchmark>();
        SecureEntityId = SecureEntityIdUtils.GenerateSecure<ZEntity>(id);
        PlainEntityId = PlainEntityIdUtils.GeneratePlain<ZEntity>(id);
    }

    private static SourceKnownIdUtils Utils { get; }
    private static SourceKnownEntityIdUtils SecureEntityIdUtils { get; }
    private static SourceKnownEntityIdUtils PlainEntityIdUtils { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId PlainEntityId { get; }
    private static ZEntity Entity { get; } = new(5);

    // --- Baseline benchmarks (non-saturating) ---

    [Benchmark]
    public long RandomLong() => BinaryPrimitives.ReadInt64LittleEndian(RandomNumberGenerator.GetBytes(8));

    [Benchmark]
    public Guid RandomGuidV4() => Guid.NewGuid();

    [Benchmark]
    public Guid RandomGuidV7() => Guid.CreateVersion7();

    [Benchmark]
    public long TimeStampManager_TimeStamp() => TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);

    // --- Sequence-dependent benchmarks (saturating) ---

    [Benchmark]
    public SequenceTimeScopedId SequenceManager_TimeScopedId() => SequenceManager<ZEntity>.GetTimeScopedId();

    [Benchmark]
    public long SourceKnownId() => Utils.Next<SourceKnownIdUtilsSaturationBenchmark>();

    // --- Non-secure SourceKnownEntityId (saturating — consumes sequence IDs) ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdPlainWithId()
        => PlainEntityIdUtils.GeneratePlain<ZEntity>(Utils.Next<SourceKnownIdUtilsSaturationBenchmark>());

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdPlainWithEntity()
        => PlainEntityIdUtils.GeneratePlain(new ZEntity(Utils.Next<SourceKnownIdUtilsSaturationBenchmark>()));

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdPlainWithProvidedLongValue()
        => PlainEntityIdUtils.GeneratePlain(Entity);

    // --- Secure SourceKnownEntityId (saturating — consumes sequence IDs) ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdSecure()
        => SecureEntityIdUtils.GenerateSecure<ZEntity>(Utils.Next<SourceKnownIdUtilsSaturationBenchmark>());

    // --- Parse benchmarks (non-saturating — operate on pre-generated IDs) ---

    [Benchmark]
    public SourceKnownEntityId ParseSecureSourceKnownEntityId()
        => SecureEntityIdUtils.Parse(SecureEntityId.EntityId);

    [Benchmark]
    public SourceKnownEntityId ParsePlainSourceKnownEntityId()
        => PlainEntityIdUtils.Parse(PlainEntityId.EntityId);

    // --- Tier conversion benchmarks (non-saturating — reuse existing SKID) ---

    [Benchmark]
    public SourceKnownEntityId ToSecure() => SecureEntityIdUtils.ToSecure(PlainEntityId);

    [Benchmark]
    public SourceKnownEntityId ToPlain() => PlainEntityIdUtils.ToPlain(SecureEntityId);
}

[EntityType(93)]
public class ZEntity(long id) : SourceKnownEntity(id);
