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
[WarmupCount(40)]
[IterationCount(40)]
[InvocationCount(786_432)] // 3× sequence cap (2^18 = 262,144) — guarantees backpressure per iteration
public class SourceKnownIdUtilsSaturationBenchmark
{
    [IterationSetup]
    public void IterationWait() => Thread.Sleep(TimeStampManager.PrecisionUnitInMsSafeDelay); // Let SequenceTimeScope reset between iterations (one tick)

    static SourceKnownIdUtilsSaturationBenchmark()
    {
        IdUtils = new(AppSettings.Development(), new EpochTimeUtils());

        var appSettings = AppSettings.Development();
        EntityIdUtils = new(appSettings, IdUtils);

        // Pre-generate GUIDs for Parse benchmarks — avoids measuring ID generation in parse benchmarks
        var id = IdUtils.Next<SourceKnownIdUtilsBenchmark>();
        SecureEntityId = EntityIdUtils.GenerateSecure<YEntity>(id);
        PlainEntityId = EntityIdUtils.GeneratePlain<YEntity>(id);
    }

    private static SourceKnownIdUtils IdUtils { get; }
    private static SourceKnownEntityIdUtils EntityIdUtils { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId PlainEntityId { get; }
    private static YEntity Entity { get; } = new(5);

    // --- Baseline benchmarks ---

    [Benchmark]
    public long RandomLong() => BinaryPrimitives.ReadInt64LittleEndian(RandomNumberGenerator.GetBytes(8));

    [Benchmark]
    public Guid RandomGuidV4() => Guid.NewGuid();

    [Benchmark]
    public Guid RandomGuidV7() => Guid.CreateVersion7();

    [Benchmark]
    public long TimeStampManager_TimeStamp() => TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);

    [Benchmark] //todo TimeScopedId look like a bottleneck, review it for possible improvements
    public SequenceTimeScopedId SequenceManager_TimeScopedId() => SequenceManager<YEntity>.GetTimeScopedId();

    // --- SourceKnownId (raw long) ---

    [Benchmark]
    public long SourceKnownId() => IdUtils.Next<SourceKnownIdUtilsBenchmark>();

    // --- Non-secure SourceKnownEntityId: BLAKE3 MAC only (explicit call variants) ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdWithProvidedSkid()
        => EntityIdUtils.GeneratePlain(Entity);

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdWithSkidGeneration()
        => EntityIdUtils.GeneratePlain<YEntity>(IdUtils.Next<SourceKnownIdUtilsBenchmark>());

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdWithEntityAllocation()
        => EntityIdUtils.GeneratePlain(new YEntity(IdUtils.Next<SourceKnownIdUtilsBenchmark>()));

    // --- Secure SourceKnownEntityId: BLAKE3 MAC + AES-256-ECB encryption ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdSecure()
        => EntityIdUtils.GenerateSecure<YEntity>(IdUtils.Next<SourceKnownIdUtilsBenchmark>());

    // --- Parse: non-secure GUID (MAC verify only) ---

    [Benchmark]
    public SourceKnownEntityId ParseSourceKnownEntityId()
        => EntityIdUtils.Parse(PlainEntityId.EntityId);

    // --- Parse: secure GUID (AES-ECB decrypt + MAC verify) ---

    [Benchmark]
    public SourceKnownEntityId ParseSecureSourceKnownEntityId()
        => EntityIdUtils.Parse(SecureEntityId.EntityId);

    // --- Tier conversion: measures encryption/decryption cost independently ---

    [Benchmark]
    public SourceKnownEntityId ToPlain() => EntityIdUtils.ToPlain(SecureEntityId);

    [Benchmark]
    public SourceKnownEntityId ToSecure() => EntityIdUtils.ToSecure(PlainEntityId);
}

[EntityType(93)]
public class ZEntity(long id) : SourceKnownEntity(id);