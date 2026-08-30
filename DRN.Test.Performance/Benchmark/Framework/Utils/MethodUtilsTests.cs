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
    private const string SampleArg = "payload";
    private static readonly object?[] CachedArgs = [SampleArg];

    private static readonly MethodInfo StaticNonGenericMethodInfo = TargetType.FindMethod(nameof(GetStatic), 1, BindingFlag.StaticPublic);
    private static readonly MethodInfo StaticGenericMethodInfo = TargetType.FindMethod(nameof(GetStatic), TypeArgs, 1, BindingFlag.StaticPublic);
    private static readonly MethodInfo InstanceNonGenericMethodInfo = TargetType.FindMethod(nameof(GetInstance), 1, BindingFlag.InstancePublic);
    private static readonly MethodInfo InstanceGenericMethodInfo = TargetType.FindMethod(nameof(GetInstance), TypeArgs, 1, BindingFlag.InstancePublic);

    private static readonly MethodInvoker StaticNonGenericInvoker = MethodInvoker.Create(StaticNonGenericMethodInfo);
    private static readonly MethodInvoker StaticGenericInvoker = MethodInvoker.Create(StaticGenericMethodInfo);
    private static readonly MethodInvoker InstanceNonGenericInvoker = MethodInvoker.Create(InstanceNonGenericMethodInfo);
    private static readonly MethodInvoker InstanceGenericInvoker = MethodInvoker.Create(InstanceGenericMethodInfo);

    private static readonly Func<string, string> StronglyTypedDelegate = StaticNonGenericMethodInfo.CreateDelegate<Func<string, string>>();

    // --- 1. Method Discovery ---

    [Benchmark(Baseline = true, Description = "MethodUtils: Type.FindMethod (Cached)")]
    [BenchmarkCategory("DiscoveryNonGeneric")]
    public MethodInfo Discovery_NonGeneric_Cached() => TargetType.FindMethod(nameof(GetStatic), 1, BindingFlag.StaticPublic);

    [Benchmark(Description = "MethodUtils: Type.FindMethodUncached")]
    [BenchmarkCategory("DiscoveryNonGeneric")]
    public MethodInfo Discovery_NonGeneric_Uncached() => TargetType.FindMethodUncached(nameof(GetStatic), 1, BindingFlag.StaticPublic);

    [Benchmark(Baseline = true, Description = "MethodUtils: Type.FindMethod<T> (Cached)")]
    [BenchmarkCategory("DiscoveryGeneric")]
    public MethodInfo Discovery_Generic_Cached() => TargetType.FindMethod(nameof(GetStatic), TypeArgs, 1, BindingFlag.StaticPublic);

    [Benchmark(Description = "MethodUtils: Type.FindMethodUncached<T>")]
    [BenchmarkCategory("DiscoveryGeneric")]
    public MethodInfo Discovery_Generic_Uncached() => TargetType.FindMethodUncached(nameof(GetStatic), TypeArgs, 1, BindingFlag.StaticPublic);

    // --- 2. Static Non-Generic Invocation ---

    [Benchmark(Baseline = true, Description = "Baseline (C#): Direct Call GetStatic(arg)")]
    [BenchmarkCategory("StaticNonGeneric")]
    public string Static_NonGeneric_Direct() => GetStatic(SampleArg);

    [Benchmark(Description = "MethodUtils: Type.InvokeStaticMethod")]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_InvokeMethod() => TargetType.InvokeStaticMethod(nameof(GetStatic), SampleArg);

    [Benchmark(Description = "Baseline (.NET): Pre-Resolved MethodInvoker.Invoke")]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_MethodInvoker() => StaticNonGenericInvoker.Invoke(null, SampleArg);

    [Benchmark(Description = "Baseline (.NET): Strongly-Typed Delegate")]
    [BenchmarkCategory("StaticNonGeneric")]
    public string Static_NonGeneric_Delegate() => StronglyTypedDelegate(SampleArg);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke(cachedArray)")]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_ReflectionInvoke_CachedArray() => StaticNonGenericMethodInfo.Invoke(null, CachedArgs);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke(new array)")]
    [BenchmarkCategory("StaticNonGeneric")]
    public object? Static_NonGeneric_ReflectionInvoke_NewArray() => StaticNonGenericMethodInfo.Invoke(null, [SampleArg]);

    // --- 3. Static Generic Invocation ---

    [Benchmark(Baseline = true, Description = "Baseline (C#): Direct Call GetStatic<T>(arg)")]
    [BenchmarkCategory("StaticGeneric")]
    public string Static_Generic_Direct() => GetStatic<MethodUtilsBenchmark>(SampleArg);

    [Benchmark(Description = "MethodUtils: Type.InvokeStaticMethod<T>")]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_InvokeMethod() => TargetType.InvokeStaticMethod(nameof(GetStatic), TypeArgs, SampleArg);

    [Benchmark(Description = "Baseline (.NET): Pre-Resolved MethodInvoker.Invoke<T>")]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_MethodInvoker() => StaticGenericInvoker.Invoke(null, SampleArg);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke<T>(cachedArray)")]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_ReflectionInvoke_CachedArray() => StaticGenericMethodInfo.Invoke(null, CachedArgs);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke<T>(new array)")]
    [BenchmarkCategory("StaticGeneric")]
    public object? Static_Generic_ReflectionInvoke_NewArray() => StaticGenericMethodInfo.Invoke(null, [SampleArg]);

    // --- 4. Instance Non-Generic Invocation ---

    [Benchmark(Baseline = true, Description = "Baseline (C#): Direct Call instance.GetInstance(arg)")]
    [BenchmarkCategory("InstanceNonGeneric")]
    public string Instance_NonGeneric_Direct() => TargetInstance.GetInstance(SampleArg);

    [Benchmark(Description = "MethodUtils: instance.InvokeMethod")]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_InvokeMethod() => TargetInstance.InvokeMethod(nameof(GetInstance), SampleArg);

    [Benchmark(Description = "Baseline (.NET): Pre-Resolved MethodInvoker.Invoke")]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_MethodInvoker() => InstanceNonGenericInvoker.Invoke(TargetInstance, SampleArg);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke(cachedArray)")]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_ReflectionInvoke_CachedArray() => InstanceNonGenericMethodInfo.Invoke(TargetInstance, CachedArgs);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke(new array)")]
    [BenchmarkCategory("InstanceNonGeneric")]
    public object? Instance_NonGeneric_ReflectionInvoke_NewArray() => InstanceNonGenericMethodInfo.Invoke(TargetInstance, [SampleArg]);

    // --- 5. Instance Generic Invocation ---

    [Benchmark(Baseline = true, Description = "Baseline (C#): Direct Call instance.GetInstance<T>(arg)")]
    [BenchmarkCategory("InstanceGeneric")]
    public string Instance_Generic_Direct() => TargetInstance.GetInstance<MethodUtilsBenchmark>(SampleArg);

    [Benchmark(Description = "MethodUtils: instance.InvokeMethod<T>")]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_InvokeMethod() => TargetInstance.InvokeMethod(nameof(GetInstance), TypeArgs, SampleArg);

    [Benchmark(Description = "Baseline (.NET): Pre-Resolved MethodInvoker.Invoke<T>")]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_MethodInvoker() => InstanceGenericInvoker.Invoke(TargetInstance, SampleArg);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke<T>(cachedArray)")]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_ReflectionInvoke_CachedArray() => InstanceGenericMethodInfo.Invoke(TargetInstance, CachedArgs);

    [Benchmark(Description = "Alternative (Reflection): MethodInfo.Invoke<T>(new array)")]
    [BenchmarkCategory("InstanceGeneric")]
    public object? Instance_Generic_ReflectionInvoke_NewArray() => InstanceGenericMethodInfo.Invoke(TargetInstance, [SampleArg]);

    // --- Target Methods ---

    public static string GetStatic(string a) => a;
    public static string GetStatic<T>(string b) => b;

    public string GetInstance(string a) => a;
    public string GetInstance<T>(string b) => b;
}
