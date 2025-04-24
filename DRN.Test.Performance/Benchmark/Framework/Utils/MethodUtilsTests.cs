using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DRN.Framework.Utils.Extensions;
using Xunit.Abstractions;

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

public class MethodUtilsBenchmark
{
    private static readonly Type Type = typeof(MethodUtilsBenchmark);

    //Todo benchmark instance methods

    [Benchmark]
    public object? NonGenericDirect() => Get();
    
    [Benchmark]
    public MethodInfo NonGenericCached() => Type.FindNonGenericMethod("Get", 0, BindingFlag.StaticPublic);

    [Benchmark]
    public MethodInfo NonGenericUnCached() => Type.FindNonGenericMethodUncached("Get", 0, BindingFlag.StaticPublic);

    [Benchmark]
    public object? GenericDirect() => Get<MethodUtilsBenchmark>();

    [Benchmark]
    public MethodInfo GenericCached() => Type.FindGenericMethod("Get", [Type], 0, BindingFlag.StaticPublic);

    [Benchmark]
    public MethodInfo GenericUnCached() => Type.FindGenericMethodUncached("Get", [Type], 0, BindingFlag.StaticPublic);

    [Benchmark]
    public object? InvokeNonGenericCached() => Type.InvokeStaticMethod("Get");

    [Benchmark]
    public object? InvokeGenericCached() => Type.InvokeStaticGenericMethod("Get", Type);

    public static object? Get<T>() => null;
    public static object? Get() => null;
}