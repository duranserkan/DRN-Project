using System.Security.Cryptography;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Blake3;
using Xunit.Abstractions;

namespace DRN.Test.Performance.Benchmark.Other.Hash;

[MemoryDiagnoser]
public class HmacPerformanceTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<HashBenchmark>(config);

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

public class HashBenchmark
{
    private byte[] _data4B = new byte[4];
    private byte[] _data8B = new byte[8];
    private byte[] _data12B = new byte[12];
    private byte[] _data16B = new byte[16];
    private byte[] _data32B = new byte[32];
    private byte[] _data64B = new byte[64];
    private byte[] _data128B = new byte[128];
    private byte[] _data256B = new byte[256];
    private byte[] _data512B = new byte[512];
    private byte[] _data1KB = new byte[1024];
    private byte[] _data2KB = new byte[2048];
    private byte[] _data4KB = new byte[4096];
    private byte[] _data8KB = new byte[8192];
    private byte[] _data16KB = new byte[16384];
    private byte[] _data32KB = new byte[32768];
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
        RandomNumberGenerator.Fill(_data4B);
        RandomNumberGenerator.Fill(_data8B);
        RandomNumberGenerator.Fill(_data12B);
        RandomNumberGenerator.Fill(_data16B);
        RandomNumberGenerator.Fill(_data32B);
        RandomNumberGenerator.Fill(_data64B);
        RandomNumberGenerator.Fill(_data128B);
        RandomNumberGenerator.Fill(_data256B);
        RandomNumberGenerator.Fill(_data512B);
        RandomNumberGenerator.Fill(_data1KB);
        RandomNumberGenerator.Fill(_data2KB);
        RandomNumberGenerator.Fill(_data4KB);
        RandomNumberGenerator.Fill(_data8KB);
        RandomNumberGenerator.Fill(_data16KB);
        RandomNumberGenerator.Fill(_data32KB);
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
        4 => _data4B,
        8 => _data8B,
        12 => _data12B,
        16 => _data16B,
        32 => _data32B,
        64 => _data64B,
        128 => _data128B,
        256 => _data256B,
        512 => _data512B,
        1024 => _data1KB,
        2048 => _data2KB,
        4096 => _data4KB,
        8192 => _data8KB,
        16384 => _data16KB,
        32768 => _data32KB,
        1048576 => _data1MB,
        10485760 => _data10MB,
        _ => throw new ArgumentException("Invalid size")
    };
}