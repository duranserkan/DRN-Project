using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AwesomeAssertions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Extensions;

namespace DRN.Test.Performance.Benchmark.Framework.Utils;

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
[SuppressMessage("ReSharper", "UnusedTypeParameter")]
public class MethodUtilsBenchmark
{
    private static readonly Type TargetType = typeof(MethodUtilsBenchmark);
    private static readonly Type[] TypeArgs = [TargetType];
    private static readonly MethodUtilsBenchmark TargetInstance = new();

    private static readonly MethodInfo StaticNonGenericMethodInfo = TargetType.FindMethod(nameof(GetStatic), 1, BindingFlag.StaticPublic);
    private static readonly MethodInfo StaticGenericMethodInfo = TargetType.FindMethod(nameof(GetStatic), TypeArgs, 1, BindingFlag.StaticPublic);
    private static readonly MethodInfo InstanceNonGenericMethodInfo = TargetType.FindMethod(nameof(GetInstance), 1, BindingFlag.InstancePublic);
    private static readonly MethodInfo InstanceGenericMethodInfo = TargetType.FindMethod(nameof(GetInstance), TypeArgs, 1, BindingFlag.InstancePublic);

    private static readonly Func<int, object?> StronglyTypedDelegate = StaticNonGenericMethodInfo.CreateDelegate<Func<int, object?>>();

    // --- 1. Method Discovery ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Discovery")]
    public MethodInfo Discovery_NonGeneric_Cached() => TargetType.FindMethod(nameof(GetStatic), 1, BindingFlag.StaticPublic);

    [Benchmark]
    [BenchmarkCategory("Discovery")]
    public MethodInfo Discovery_NonGeneric_Uncached() => TargetType.FindMethodUncached(nameof(GetStatic), 1, BindingFlag.StaticPublic);

    [Benchmark]
    [BenchmarkCategory("Discovery")]
    public MethodInfo Discovery_Generic_Cached() => TargetType.FindMethod(nameof(GetStatic), TypeArgs, 1, BindingFlag.StaticPublic);

    [Benchmark]
    [BenchmarkCategory("Discovery")]
    public MethodInfo Discovery_Generic_Uncached() => TargetType.FindMethodUncached(nameof(GetStatic), TypeArgs, 1, BindingFlag.StaticPublic);

    // --- 2. Static Non-Generic Invocation ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StaticNonGeneric")]
    public object Static_NonGeneric_Direct() => GetStatic(42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_Delegate() => StronglyTypedDelegate(42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_InvokeFast() => StaticNonGenericMethodInfo.InvokeFast(null, 42);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_ReflectionInvoke() => StaticNonGenericMethodInfo.Invoke(null, [42]);

    [Benchmark]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_InvokeMethod() => TargetType.InvokeStaticMethod(nameof(GetStatic), 42);

    // --- 3. Static Generic Invocation ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StaticGeneric")]
    public object Static_Generic_Direct() => GetStatic<MethodUtilsBenchmark>(42);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_InvokeFast() => StaticGenericMethodInfo.InvokeFast(null, 42);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_ReflectionInvoke() => StaticGenericMethodInfo.Invoke(null, [42]);

    [Benchmark]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_InvokeMethod() => TargetType.InvokeStaticMethod(nameof(GetStatic), TypeArgs, 42);

    // --- 4. Instance Non-Generic Invocation ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object Instance_NonGeneric_Direct() => TargetInstance.GetInstance(42);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_InvokeFast() => InstanceNonGenericMethodInfo.InvokeFast(TargetInstance, 42);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_ReflectionInvoke() => InstanceNonGenericMethodInfo.Invoke(TargetInstance, [42]);

    [Benchmark]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_InvokeMethod() => TargetInstance.InvokeMethod(nameof(GetInstance), 42);

    // --- 5. Instance Generic Invocation ---

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InstanceGeneric")]
    public object Instance_Generic_Direct() => TargetInstance.GetInstance<MethodUtilsBenchmark>(42);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_InvokeFast() => InstanceGenericMethodInfo.InvokeFast(TargetInstance, 42);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_ReflectionInvoke() => InstanceGenericMethodInfo.Invoke(TargetInstance, [42]);

    [Benchmark]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_InvokeMethod() => TargetInstance.InvokeMethod(nameof(GetInstance), TypeArgs, 42);

    // --- Target Methods ---

    public static object GetStatic(int a) => a;
    public static object GetStatic<T>(int b) => b;

    public object GetInstance(int a) => a;
    public object GetInstance<T>(int b) => b;
}
