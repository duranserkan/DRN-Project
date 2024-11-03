using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Testing.Contexts.Startup;

public static class StartupJobRunner
{
    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);

    public static TestStartupResult Result { get; private set; } = null!;

    public static void TriggerStartupJobs(MethodInfo testMethod, Type type)
    {
        if (_triggered || type != typeof(TestContext)) return;

        StartupLock.Wait();
        try
        {
            if (_triggered) return;
            Trigger(testMethod);
            _triggered = true;
        }
        finally
        {
            StartupLock.Release();
        }
    }

    private static void Trigger(MethodInfo testMethod)
    {
        var startedAt = DateTimeOffset.Now;
        _ = JsonConventions.DefaultOptions; // to trigger static ctor that replaces .net defaults with better one
        TestEnvironment.TestContextEnabled = true;

        var jobTypes = GetTestStartupJobTypes(testMethod);
        foreach (var startupJobType in jobTypes)
        {
            var startupJob = (ITestStartupJob)Activator.CreateInstance(startupJobType)!;
            using var startupContext = new StartupContext(startupJob);
            startupJob.RunAsync(startupContext).GetAwaiter().GetResult();
        }

        Result = new TestStartupResult(startedAt, DateTimeOffset.Now, testMethod, jobTypes);
    }

    private static Type[] GetTestStartupJobTypes(MethodInfo testMethod)
    {
        var testAssembly = testMethod.ReflectedType!.Assembly;
        var jobs = testAssembly.GetTypesAssignableTo(typeof(ITestStartupJob));

        return jobs;
    }
}

public class TestStartupResult
{
    public static TestStartupResult? Value { get; private set; }

    public TestStartupResult(DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        MethodInfo triggeredBy,
        IReadOnlyList<Type> startupJobs)
    {
        Value ??= this;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        TriggeredBy = triggeredBy;
        StartupJobs = startupJobs;
    }

    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset CompletedAt { get; }
    public MethodInfo TriggeredBy { get; }
    public IReadOnlyList<Type> StartupJobs { get; }
}