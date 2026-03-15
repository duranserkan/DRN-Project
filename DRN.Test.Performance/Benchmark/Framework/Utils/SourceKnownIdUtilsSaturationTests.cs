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
[InvocationCount(6_291_456)] // 3× sequence cap (2^21 = 2,097,152) — guarantees backpressure per iteration
public class SourceKnownIdUtilsSaturationBenchmark
{
    [IterationSetup]
    public void IterationWait() => Thread.Sleep(1000); // Let SequenceTimeScope reset between iterations
    static SourceKnownIdUtilsSaturationBenchmark()
    {
        Utils = new(AppSettings.Development(), new EpochTimeUtils());

        var appSettings = AppSettings.Development();
        SecureEntityIdUtils = new(appSettings, Utils);
        UnsecureEntityIdUtils = new(appSettings, Utils);

        // Pre-generate IDs for Parse and ToSecure/ToUnsecure benchmarks — avoids measuring ID generation
        var id = Utils.Next<SourceKnownIdUtilsSaturationBenchmark>();
        SecureEntityId = SecureEntityIdUtils.GenerateSecure<ZEntity>(id);
        UnsecureEntityId = UnsecureEntityIdUtils.GenerateUnsecure<ZEntity>(id);
    }

    private static SourceKnownIdUtils Utils { get; }
    private static SourceKnownEntityIdUtils SecureEntityIdUtils { get; }
    private static SourceKnownEntityIdUtils UnsecureEntityIdUtils { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId UnsecureEntityId { get; }
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
    public SourceKnownEntityId SourceKnownEntityIdUnsecureWithId()
        => UnsecureEntityIdUtils.GenerateUnsecure<ZEntity>(Utils.Next<SourceKnownIdUtilsSaturationBenchmark>());

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdUnsecureWithEntity()
        => UnsecureEntityIdUtils.GenerateUnsecure(new ZEntity(Utils.Next<SourceKnownIdUtilsSaturationBenchmark>()));

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdUnsecureWithProvidedLongValue()
        => UnsecureEntityIdUtils.GenerateUnsecure(Entity);

    // --- Secure SourceKnownEntityId (saturating — consumes sequence IDs) ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdSecure()
        => SecureEntityIdUtils.GenerateSecure<ZEntity>(Utils.Next<SourceKnownIdUtilsSaturationBenchmark>());

    // --- Parse benchmarks (non-saturating — operate on pre-generated IDs) ---

    [Benchmark]
    public SourceKnownEntityId ParseSecureSourceKnownEntityId()
        => SecureEntityIdUtils.Parse(SecureEntityId.EntityId);

    [Benchmark]
    public SourceKnownEntityId ParseUnsecureSourceKnownEntityId()
        => UnsecureEntityIdUtils.Parse(UnsecureEntityId.EntityId);

    // --- Tier conversion benchmarks (non-saturating — reuse existing SKID) ---

    [Benchmark]
    public SourceKnownEntityId ToSecure() => SecureEntityIdUtils.ToSecure(UnsecureEntityId);

    [Benchmark]
    public SourceKnownEntityId ToUnsecure() => UnsecureEntityIdUtils.ToUnsecure(SecureEntityId);
}

[EntityType(93)]
public class ZEntity(long id) : SourceKnownEntity(id);
