using System.Security.Claims;
using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Auth;

namespace DRN.Test.Performance.Benchmark.Framework.Utils.Auth;

public class ScopedUserPerformanceTests(ITestOutputHelper output)
{
#if !DEBUG
    [Fact] // Should run on a release build.
#endif
    public void Run_Benchmarks()
    {
        var logger = new AccumulationLogger();
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddLogger(logger)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);
        var summary = BenchmarkRunner.Run<ScopedUserBenchmark>(config);
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
public class ScopedUserBenchmark
{
    private const string ClaimType = "permission-version";
    private const string Issuer = "benchmark-identity-provider";
    private ClaimsIdentity _identity = null!;
    private ScopedUser _scopedUser = null!;

    [GlobalSetup]
    public void Setup()
    {
        _identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "benchmark-user-42", ClaimValueTypes.String, Issuer),
                new Claim(ClaimTypes.Name, "Benchmark User", ClaimValueTypes.String, Issuer),
                new Claim(ClaimTypes.Email, "benchmark.user@example.test", ClaimValueTypes.Email, Issuer),
                new Claim(ClaimTypes.Role, "administrator", ClaimValueTypes.String, Issuer),
                new Claim(ClaimTypes.Role, "report-reader", ClaimValueTypes.String, Issuer),
                new Claim("tenant-id", "019f7aee-18aa-7983-965a-770f88f2992c", ClaimValueTypes.String, Issuer),
                new Claim(ClaimConventions.AuthenticationMethodReference, "mfa", ClaimValueTypes.String, Issuer),
                new Claim(ClaimTypes.AuthenticationMethod, "password", ClaimValueTypes.String, Issuer),
                new Claim(ClaimType, "41", ClaimValueTypes.Integer, Issuer)
            ],
            authenticationType: "Benchmark",
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);
        _scopedUser = ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(_identity));
    }

    [Benchmark(Baseline = true)]
    public int GetIntClaimFromIdentity() => Parse(_identity.FindFirst(ClaimType)?.Value);

    [Benchmark]
    public int GetIntClaimFromScopedUser() => _scopedUser.GetClaimParameter<int>(ClaimType, Issuer);

    private static int Parse(string? value) => int.TryParse(value, provider: null, out var result) ? result : default;
}
