using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Blake3;
using Perfolizer.Mathematics.OutlierDetection;

namespace DRN.Test.Performance.Benchmark.Other.Hash;

public class HmacPerformanceLargePayloadTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<HashBenchmarkLargePayload>(config);

        if (summary.ValidationErrors.Length > 0)
        {
            output.WriteLine("===================================");
            output.WriteLine("Validation errors");
            output.WriteLine("===================================");
            output.WriteLine(JsonSerializer.Serialize(summary.ValidationErrors));
            output.WriteLine("===================================");
        }

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
[InvocationCount(2_097_152)]
public class HashBenchmarkLargePayload
{
    private byte[] _data1MB = new byte[1048576];
    private byte[] _data10MB = new byte[10485760];
    private byte[] _key = new byte[32];

    [Params(4, 8, 12, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 1048576, 10485760)]
    public int Size { get; set; }

    private static Hasher Blake3Hasher { get; set; }
    private static Hasher Blake3HasherForUpdateJoin { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        RandomNumberGenerator.Fill(_data1MB);
        RandomNumberGenerator.Fill(_data10MB);
        RandomNumberGenerator.Fill(_key);
        Blake3Hasher = Hasher.NewKeyed(_key);
        Blake3HasherForUpdateJoin = Hasher.NewKeyed(_key);
    }

    [Benchmark]
    public byte[] Blake3_256_Update()
    {
        Blake3Hasher.Update(GetData());
        var tag = Blake3Hasher.Finalize();
        Blake3Hasher.Reset();

        return tag.AsSpan().ToArray();
    }

    [Benchmark]
    public byte[] Blake3_256_UpdateWithJoin()
    {
        Blake3HasherForUpdateJoin.UpdateWithJoin(GetData());
        var tag = Blake3HasherForUpdateJoin.Finalize();
        Blake3HasherForUpdateJoin.Reset();

        return tag.AsSpan().ToArray();
    }

    [Benchmark]
    public byte[] Blake3_256_New_Update()
    {
        using var hasher = Hasher.NewKeyed(_key);

        hasher.Update(GetData());
        var tag = hasher.Finalize();

        return tag.AsSpan().ToArray();
    }

    [Benchmark]
    public byte[] Blake3_256_New_Update_With_Join()
    {
        using var hasher = Hasher.NewKeyed(_key);

        hasher.UpdateWithJoin(GetData());
        var tag = hasher.Finalize();

        return tag.AsSpan().ToArray();
    }

    [Benchmark]
    public byte[] Fast_Crc32() => Crc32.Hash(GetData());

    [Benchmark]
    public byte[] Fast_Crc64() => Crc64.Hash(GetData());

    [Benchmark]
    public byte[] Fast_XxHash3() => XxHash3.Hash(GetData());

    [Benchmark]
    public byte[] Fast_XxHash32() => XxHash32.Hash(GetData());

    [Benchmark]
    public byte[] Fast_XxHash64() => XxHash64.Hash(GetData());

    [Benchmark]
    public byte[] Fast_XxHash128() => XxHash128.Hash(GetData());

    [Benchmark]
    public byte[] Hmac_Sha256() => HMACSHA256.HashData(_key, GetData());

    [Benchmark]
    public byte[] Hmac_Sha512() => HMACSHA512.HashData(_key, GetData());

    //todo KMAC, HMACSHA3_256, HMACSHA3_512(not available for macos yet) performance benchmark

    //Not supported yet on macos
    // [Benchmark]
    // public byte[] HmacSha3_Sha256() => HMACSHA3_256.HashData(_key, GetData());
    // [Benchmark]
    // public byte[] HmacSha3_Sha2512() => HMACSHA3_512.HashData(_key, GetData());
    // [Benchmark]
    // public byte[] Kmac_128() => Kmac128.HashData(_key, GetData(), 32);
    // [Benchmark]
    // public byte[] Kmac_256() => Kmac256.HashData(_key, GetData(), 64);

    private byte[] GetData() => Size switch
    {
        1048576 => _data1MB,
        10485760 => _data10MB,
        _ => throw new ArgumentException("Invalid size")
    };
}