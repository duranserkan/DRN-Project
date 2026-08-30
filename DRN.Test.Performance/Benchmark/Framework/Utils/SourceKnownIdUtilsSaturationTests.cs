using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
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
        summary.Reports.Should().NotBeEmpty();

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
    public static void IterationWait() => Thread.Sleep(TimeStampManager.PrecisionUnitInMsSafeDelay); // Let SequenceTimeScope reset between iterations (one tick)

    static SourceKnownIdUtilsSaturationBenchmark()
    {
        IdUtils = new(AppSettings.Development(), new EpochTimeUtils());

        var appSettings = AppSettings.Development();
        EntityIdUtils = new(appSettings, IdUtils);

        // Pre-generate GUIDs for Parse benchmarks — avoids measuring ID generation in parse benchmarks
        var id = IdUtils.Next<ZEntity>();
        SecureEntityId = EntityIdUtils.GenerateSecure<ZEntity>(id);
        PlainEntityId = EntityIdUtils.GeneratePlain<ZEntity>(id);
        Entity = new(IdUtils.Next<ZEntity>());
    }

    private static SourceKnownIdUtils IdUtils { get; }
    private static SourceKnownEntityIdUtils EntityIdUtils { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId PlainEntityId { get; }
    private static ZEntity Entity { get; }

    // --- Baseline benchmarks ---

    [Benchmark]
    public static long RandomLong() => BinaryPrimitives.ReadInt64LittleEndian(RandomNumberGenerator.GetBytes(8));

    [Benchmark]
    public static Guid RandomGuidV4() => Guid.NewGuid();

    [Benchmark]
    public static Guid RandomGuidV7() => Guid.CreateVersion7();

    [Benchmark]
    public static long TimeStampManager_TimeStamp() => TimeStampManager.CurrentTimestamp(EpochTimeUtils.DefaultEpoch);

    [Benchmark]
    public static SequenceTimeScopedId SequenceManager_TimeScopedId() => SequenceManager<ZEntity>.GetTimeScopedId(EpochTimeUtils.DefaultEpoch);

    // --- SourceKnownId (raw long) ---

    [Benchmark]
    public static long SourceKnownId() => IdUtils.Next<ZEntity>();

    // --- Non-secure SourceKnownEntityId: BLAKE3 MAC only (explicit call variants) ---

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdWithProvidedSkid()
        => EntityIdUtils.GeneratePlain(Entity);

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdWithSkidGeneration()
        => EntityIdUtils.GeneratePlain<ZEntity>(IdUtils.Next<ZEntity>());

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdWithEntityAllocation()
        => EntityIdUtils.GeneratePlain(new ZEntity(IdUtils.Next<ZEntity>()));

    // --- Secure SourceKnownEntityId: BLAKE3 MAC + AES-256-ECB encryption ---

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdSecure()
        => EntityIdUtils.GenerateSecure<ZEntity>(IdUtils.Next<ZEntity>());

    // --- Parse: non-secure GUID (MAC verify only) ---

    [Benchmark]
    public static SourceKnownEntityId ParseSourceKnownEntityId()
        => EntityIdUtils.Parse(PlainEntityId.EntityId);

    // --- Parse: secure GUID (AES-ECB decrypt + MAC verify) ---

    [Benchmark]
    public static SourceKnownEntityId ParseSecureSourceKnownEntityId()
        => EntityIdUtils.Parse(SecureEntityId.EntityId);

    // --- Tier conversion: measures encryption/decryption cost independently ---

    [Benchmark]
    public static SourceKnownEntityId ToPlain() => EntityIdUtils.ToPlain(SecureEntityId);

    [Benchmark]
    public static SourceKnownEntityId ToSecure() => EntityIdUtils.ToSecure(PlainEntityId);
}

[TestEntityType(93)]
public class ZEntity(long id) : SourceKnownEntity(id);
