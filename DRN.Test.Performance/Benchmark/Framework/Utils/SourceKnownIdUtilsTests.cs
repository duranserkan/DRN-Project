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
using Perfolizer.Mathematics.OutlierDetection;

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
        summary.Reports.Should().NotBeEmpty();

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
[MemoryDiagnoser]
[WarmupCount(120)]
[IterationCount(120)]
[InvocationCount(262_144)] // sequence cap (2^18) — at capacity per 250ms tick; Thread.Sleep prevents overflow
public class SourceKnownIdUtilsBenchmark
{
    [IterationSetup]
    public static void IterationWait() => Thread.Sleep(TimeStampManager.PrecisionUnitInMsSafeDelay); // Let SequenceTimeScope reset between iterations (one tick)

    static SourceKnownIdUtilsBenchmark()
    {
        IdUtils = new(AppSettings.Development(), new EpochTimeUtils());

        var appSettings = AppSettings.Development();
        EntityIdUtils = new(appSettings, IdUtils);

        // Pre-generate GUIDs for Parse benchmarks — avoids measuring ID generation in parse benchmarks
        var id = IdUtils.Next<YEntity>();
        SecureEntityId = EntityIdUtils.GenerateSecure<YEntity>(id);
        PlainEntityId = EntityIdUtils.GeneratePlain<YEntity>(id);
        Entity = new(IdUtils.Next<YEntity>());
    }

    private static SourceKnownIdUtils IdUtils { get; }
    private static SourceKnownEntityIdUtils EntityIdUtils { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId PlainEntityId { get; }
    private static YEntity Entity { get; }

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
    public static SequenceTimeScopedId SequenceManager_TimeScopedId() => SequenceManager<YEntity>.GetTimeScopedId(EpochTimeUtils.DefaultEpoch);

    // --- SourceKnownId (raw long) ---

    [Benchmark]
    public static long SourceKnownId() => IdUtils.Next<YEntity>();

    // --- Non-secure SourceKnownEntityId: BLAKE3 MAC only (explicit call variants) ---

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdWithProvidedSkid()
        => EntityIdUtils.GeneratePlain(Entity);

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdWithSkidGeneration()
        => EntityIdUtils.GeneratePlain<YEntity>(IdUtils.Next<YEntity>());

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdWithEntityAllocation()
        => EntityIdUtils.GeneratePlain(new YEntity(IdUtils.Next<YEntity>()));

    // --- Secure SourceKnownEntityId: BLAKE3 MAC + AES-256-ECB encryption ---

    [Benchmark]
    public static SourceKnownEntityId SourceKnownEntityIdSecure()
        => EntityIdUtils.GenerateSecure<YEntity>(IdUtils.Next<YEntity>());

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

[TestEntityType(92)]
public class YEntity(long id) : SourceKnownEntity(id);
