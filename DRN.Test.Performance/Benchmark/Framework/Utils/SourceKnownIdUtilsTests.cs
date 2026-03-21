using System.Buffers.Binary;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
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
[WarmupCount(1)]
[IterationCount(30)]
[InvocationCount(1_048_576)] // sequence cap (2^20) — at capacity; Thread.Sleep prevents overflow
public class SourceKnownIdUtilsBenchmark
{
    [IterationSetup]
    public void IterationWait() => Thread.Sleep(1000); // Let SequenceTimeScope reset between iterations
    static SourceKnownIdUtilsBenchmark()
    {
        Utils = new(AppSettings.Development(), new EpochTimeUtils());

        var appSettings = AppSettings.Development();
        SecureEntityIdUtils = new(appSettings, Utils);
        UnsecureEntityIdUtils = new(appSettings, Utils);

        // Pre-generate GUIDs for Parse benchmarks — avoids measuring ID generation in parse benchmarks
        var id = Utils.Next<SourceKnownIdUtilsBenchmark>();
        SecureEntityId = SecureEntityIdUtils.GenerateSecure<YEntity>(id);
        UnsecureEntityId = UnsecureEntityIdUtils.GenerateUnsecure<YEntity>(id);
    }

    private static SourceKnownIdUtils Utils { get; }
    private static SourceKnownEntityIdUtils SecureEntityIdUtils { get; }
    private static SourceKnownEntityIdUtils UnsecureEntityIdUtils { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId UnsecureEntityId { get; }
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
    public long SourceKnownId() => Utils.Next<SourceKnownIdUtilsBenchmark>();

    // --- Non-secure SourceKnownEntityId: BLAKE3 MAC only (explicit call variants) ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdUnsecureWithId()
        => UnsecureEntityIdUtils.GenerateUnsecure<YEntity>(Utils.Next<SourceKnownIdUtilsBenchmark>());

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdUnsecureWithEntity()
        => UnsecureEntityIdUtils.GenerateUnsecure(new YEntity(Utils.Next<SourceKnownIdUtilsBenchmark>()));

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdUnsecureWithProvidedLongValue()
        => UnsecureEntityIdUtils.GenerateUnsecure(Entity);

    // --- Secure SourceKnownEntityId: BLAKE3 MAC + AES-256-ECB encryption ---

    [Benchmark]
    public SourceKnownEntityId SourceKnownEntityIdSecure()
        => SecureEntityIdUtils.GenerateSecure<YEntity>(Utils.Next<SourceKnownIdUtilsBenchmark>());

    // --- Parse: secure GUID (AES-ECB decrypt + MAC verify) ---

    [Benchmark]
    public SourceKnownEntityId ParseSecureSourceKnownEntityId()
        => SecureEntityIdUtils.Parse(SecureEntityId.EntityId);

    // --- Parse: non-secure GUID (MAC verify only) ---

    [Benchmark]
    public SourceKnownEntityId ParseUnsecureSourceKnownEntityId()
        => UnsecureEntityIdUtils.Parse(UnsecureEntityId.EntityId);

    // --- Tier conversion: measures encryption/decryption cost independently ---

    [Benchmark]
    public SourceKnownEntityId ToSecure() => SecureEntityIdUtils.ToSecure(UnsecureEntityId);

    [Benchmark]
    public SourceKnownEntityId ToUnsecure() => UnsecureEntityIdUtils.ToUnsecure(SecureEntityId);
}

[EntityType(92)]
public class YEntity(long id) : SourceKnownEntity(id);

public class UnrollConfig : ManualConfig
{
    public UnrollConfig() => AddJob(Job.Default.WithUnrollFactor(16));
}