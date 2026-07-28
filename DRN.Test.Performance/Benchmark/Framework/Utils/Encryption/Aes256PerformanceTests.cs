using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Data.Encryption;

namespace DRN.Test.Performance.Benchmark.Framework.Utils.Encryption;

public class Aes256PerformanceTests(ITestOutputHelper output)
{
#if !DEBUG
    [Fact] // Should run on a release build.
#endif
    public void Run_Benchmarks()
    {
        if (!Aes256.IsSupported)
        {
            output.WriteLine("AES runtime intrinsics are unavailable; the requested implementation comparison was skipped.");
            return;
        }

        var logger = new AccumulationLogger();
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddLogger(logger)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
        var summary = BenchmarkRunner.Run<Aes256Benchmark>(config);
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

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[WarmupCount(120)]
[IterationCount(120)]
[InvocationCount(262_144)] // sequence cap (2^18) — at capacity per 250ms tick; Thread.Sleep prevents overflow
public class Aes256Benchmark
{
    private const string KeyHex = "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4";
    private const string PlaintextHex = "6BC1BEE22E409F96E93D7E117393172A";

    private Aes256 _aes = null!;
    private Vector128<byte> _ciphertext;
    private Vector128<byte> _plaintext;

    [GlobalSetup]
    public void Setup()
    {
        var key = Convert.FromHexString(KeyHex);
        try
        {
            _aes = new Aes256(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _plaintext = VectorFromHex(PlaintextHex);
        _ciphertext = _aes.EncryptWithFramework(_plaintext);
    }

    [Benchmark]
    [BenchmarkCategory("Encrypt")]
    public Vector128<byte> EncryptWithFramework() => _aes.EncryptWithFramework(_plaintext);

    [Benchmark]
    [BenchmarkCategory("Encrypt")]
    public Vector128<byte> EncryptWithFallback() => _aes.Encrypt(_plaintext);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Encrypt")]
    public Vector128<byte> EncryptRuntimeIntrinsics() => _aes.EncryptRuntimeIntrinsics(_plaintext);


    [Benchmark]
    [BenchmarkCategory("Decrypt")]
    public Vector128<byte> DecryptWithFramework() => _aes.DecryptWithFramework(_ciphertext);

    [Benchmark]
    [BenchmarkCategory("Decrypt")]
    public Vector128<byte> DecryptWithFallback() => _aes.Decrypt(_ciphertext);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Decrypt")]
    public Vector128<byte> DecryptRuntimeIntrinsics() => _aes.DecryptRuntimeIntrinsics(_ciphertext);

    [GlobalCleanup]
    public void Cleanup() => _aes.Dispose();

    private static Vector128<byte> VectorFromHex(string value)
    {
        var bytes = Convert.FromHexString(value);
        return Vector128.LoadUnsafe(ref MemoryMarshal.GetArrayDataReference(bytes));
    }
}
