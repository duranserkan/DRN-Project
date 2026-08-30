using System.Reflection;
using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Extensions;

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

[MemoryDiagnoser]
public class MethodUtilsPerformanceTests(ITestOutputHelper output)
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
        var summary = BenchmarkRunner.Run<MethodUtilsBenchmark>(config);
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
[WarmupCount(30)]
[IterationCount(30)]
[InvocationCount(100_000)]
public class MethodUtilsBenchmark
{
    private static readonly Type Type = typeof(MethodUtilsBenchmark);
    private static readonly MethodUtilsBenchmark Instance = new();

    private static readonly MethodInfo StaticNonGenericMethodInfo = Type.FindNonGenericMethod(nameof(GetStatic), 1, BindingFlag.StaticPublic);
    private static readonly MethodInfo StaticGenericMethodInfo = Type.FindGenericMethod(nameof(GetStatic), [Type], 1, BindingFlag.StaticPublic);
    private static readonly MethodInfo InstanceNonGenericMethodInfo = Type.FindNonGenericMethod(nameof(GetInstance), 1, BindingFlag.InstancePublic);
    private static readonly MethodInfo InstanceGenericMethodInfo = Type.FindGenericMethod(nameof(GetInstance), [Type], 1, BindingFlag.InstancePublic);

    private static readonly Func<int, object?> StronglyTypedDelegate = StaticNonGenericMethodInfo.CreateDelegate<Func<int, object?>>();

    // --- Method Resolution Category ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MethodDiscovery")]
    public MethodInfo FindNonGenericCached() => Type.FindNonGenericMethod(nameof(GetStatic), 1, BindingFlag.StaticPublic);

    [Benchmark]
    [BenchmarkCategory("MethodDiscovery")]
    public MethodInfo FindNonGenericUncached() => Type.FindNonGenericMethodUncached(nameof(GetStatic), 1, BindingFlag.StaticPublic);

    [Benchmark]
    [BenchmarkCategory("MethodDiscovery")]
    public MethodInfo FindGenericCached() => Type.FindGenericMethod(nameof(GetStatic), [Type], 1, BindingFlag.StaticPublic);

    [Benchmark]
    [BenchmarkCategory("MethodDiscovery")]
    public MethodInfo FindGenericUncached() => Type.FindGenericMethodUncached(nameof(GetStatic), [Type], 1, BindingFlag.StaticPublic);

    // --- Static Non-Generic Category ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? StaticNonGeneric_Direct() => GetStatic(42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? StaticNonGeneric_StronglyTypedDelegate() => StronglyTypedDelegate(42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? StaticNonGeneric_Preresolved_InvokeFast() => StaticNonGenericMethodInfo.InvokeFast(null, 42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? StaticNonGeneric_Preresolved_Invoke() => StaticNonGenericMethodInfo.Invoke(null, [42]);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? StaticNonGeneric_InvokeFast() => Type.InvokeStaticMethodFast(nameof(GetStatic), 42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? StaticNonGeneric_Invoke() => Type.InvokeStaticMethod(nameof(GetStatic), 42);

    // --- Static Generic Category ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StaticGeneric")]
    public object? StaticGeneric_Direct() => GetStatic<MethodUtilsBenchmark>(42);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? StaticGeneric_Preresolved_InvokeFast() => StaticGenericMethodInfo.InvokeFast(null, 42);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? StaticGeneric_Preresolved_Invoke() => StaticGenericMethodInfo.Invoke(null, [42]);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? StaticGeneric_InvokeFast() => Type.InvokeStaticGenericMethodFast(nameof(GetStatic), [Type], 42);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? StaticGeneric_Invoke() => Type.InvokeStaticGenericMethod(nameof(GetStatic), [Type], 42);

    // --- Instance Non-Generic Category ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? InstanceNonGeneric_Direct() => Instance.GetInstance(42);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? InstanceNonGeneric_Preresolved_InvokeFast() => InstanceNonGenericMethodInfo.InvokeFast(Instance, 42);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? InstanceNonGeneric_Preresolved_Invoke() => InstanceNonGenericMethodInfo.Invoke(Instance, [42]);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? InstanceNonGeneric_InvokeFast() => Instance.InvokeMethodFast(nameof(GetInstance), 42);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? InstanceNonGeneric_Invoke() => Instance.InvokeMethod(nameof(GetInstance), 42);

    // --- Instance Generic Category ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InstanceGeneric")]
    public object? InstanceGeneric_Direct() => Instance.GetInstance<MethodUtilsBenchmark>(42);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? InstanceGeneric_Preresolved_InvokeFast() => InstanceGenericMethodInfo.InvokeFast(Instance, 42);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? InstanceGeneric_Preresolved_Invoke() => InstanceGenericMethodInfo.Invoke(Instance, [42]);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? InstanceGeneric_InvokeFast() => Instance.InvokeGenericMethodFast(nameof(GetInstance), [Type], 42);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? InstanceGeneric_Invoke() => Instance.InvokeGenericMethod(nameof(GetInstance), [Type], 42);

    // --- Target Methods ---

    public static object? GetStatic(int a) => a;
    public static object? GetStatic<T>(int b) => b;

    public object? GetInstance(int a) => a;
    public object? GetInstance<T>(int b) => b;
}
