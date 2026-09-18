using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Testing.Providers;
using DRN.Framework.Utils.Time;
using DRN.Test.Performance.Benchmark.Domain;
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
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[WarmupCount(120)]
[IterationCount(120)]
[InvocationCount(262_144)] // sequence cap (2^18) — at capacity per 250ms tick; Thread.Sleep prevents overflow
public class SourceKnownIdUtilsBenchmark
{
    [IterationSetup]
    public void IterationWait() => Thread.Sleep(TimeStampManager.PrecisionUnitInMsSafeDelay); // Let SequenceTimeScope reset between iterations (one tick)

    static SourceKnownIdUtilsBenchmark()
    {
        var appSettings = SettingsProvider.Development();
        IdUtils = new(appSettings);
        EntityIdUtils = new(appSettings, IdUtils);

        // Pre-generate IDs for Parse benchmarks — avoids measuring ID generation in parse benchmarks
        Skid = IdUtils.Next<PerformanceTestEntity>();
        SecureEntityId = EntityIdUtils.GenerateSecure<PerformanceTestEntity>(Skid);
        PlainEntityId = EntityIdUtils.GeneratePlain<PerformanceTestEntity>(Skid);
    }

    private static SourceKnownIdUtils IdUtils { get; }
    private static SourceKnownEntityIdUtils EntityIdUtils { get; }
    private static long Skid { get; }
    private static SourceKnownEntityId SecureEntityId { get; }
    private static SourceKnownEntityId PlainEntityId { get; }
    
    // --- 64-Bit ID Generation & Baselines ---

    [Benchmark(Baseline = true, Description = "SKID generation")]
    [BenchmarkCategory("1: Generation 64 Bit")]
    public long SourceKnownId() => IdUtils.Next<PerformanceTestEntity>();

    [Benchmark(Description = "Random 64-bit integer generation")]
    [BenchmarkCategory("1: Generation 64 Bit")]
    public long RandomLong() => BinaryPrimitives.ReadInt64LittleEndian(RandomNumberGenerator.GetBytes(8));

    // --- 128-Bit GUID Generation ---

    [Benchmark(Baseline = true, Description = "Plain SKEID generation")]
    [BenchmarkCategory("2: Generation 128 Bit")]
    public SourceKnownEntityId SourceKnownEntityIdWithSkidGeneration()
        => EntityIdUtils.GeneratePlain<PerformanceTestEntity>(IdUtils.Next<PerformanceTestEntity>());

    [Benchmark(Description = "Secure SKEID generation")]
    [BenchmarkCategory("2: Generation 128 Bit")]
    public SourceKnownEntityId SourceKnownEntityIdSecure()
        => EntityIdUtils.GenerateSecure<PerformanceTestEntity>(IdUtils.Next<PerformanceTestEntity>());

    [Benchmark(Description = "Random UUID V4 generation")]
    [BenchmarkCategory("2: Generation 128 Bit")]
    public Guid RandomGuidV4() => Guid.NewGuid();

    [Benchmark(Description = "Random UUID V7 generation")]
    [BenchmarkCategory("2: Generation 128 Bit")]
    public Guid RandomGuidV7() => Guid.CreateVersion7();

    // --- Parsing ---

    [Benchmark(Baseline = true, Description = "SKID parsing")]
    [BenchmarkCategory("3: Parsing")]
    public SourceKnownId ParseSourceKnownId() => IdUtils.Parse(Skid);

    [Benchmark(Description = "SKEID parsing")]
    [BenchmarkCategory("3: Parsing")]
    public SourceKnownEntityId ParseSourceKnownEntityId()
        => EntityIdUtils.Parse(PlainEntityId.EntityId, SourceKnownEntityIdFormat.Plain);

    [Benchmark(Description = "Secure SKEID parsing")]
    [BenchmarkCategory("3: Parsing")]
    public SourceKnownEntityId ParseSecureSourceKnownEntityId()
        => EntityIdUtils.Parse(SecureEntityId.EntityId, SourceKnownEntityIdFormat.Secure);

    // --- Conversion ---

    [Benchmark(Baseline = true, Description = "ToPlain")]
    [BenchmarkCategory("4: Conversion")]
    public SourceKnownEntityId ToPlain() => EntityIdUtils.ToPlain(SecureEntityId);

    [Benchmark(Description = "ToSecure")]
    [BenchmarkCategory("4: Conversion")]
    public SourceKnownEntityId ToSecure() => EntityIdUtils.ToSecure(PlainEntityId);
}
