using DRN.Framework.SharedKernel.Conventions;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Testing.Contexts.Startup;

public static class StartupJobRunner
{
    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);
    public static StartupResult? StartupResult { get; private set; }

    public static void TriggerStartupJobs(MethodInfo testMethod, Type type)
    {
        if (type == typeof(StartupContext)) return;

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
        typeof(TestEnvironment)
            .GetProperty(nameof(TestEnvironment.TestContextEnabled), BindingFlag.StaticPublic)!
            .SetValue(null, true);

        var jobTypes = GetTestStartupJobTypes(testMethod);
        foreach (var startupJobType in jobTypes)
        {
            var startupJob = (ITestStartupJob)Activator.CreateInstance(startupJobType)!;
            startupJob.Run(new StartupContext(startupJob));
        }

        var completedAt = DateTimeOffset.Now;
        StartupResult = new StartupResult(startedAt, completedAt, testMethod, jobTypes);
    }

    private static Type[] GetTestStartupJobTypes(MethodInfo testMethod)
    {
        var testAssembly = testMethod.ReflectedType!.Assembly;
        var jobs = testAssembly.GetTypesAssignableTo(typeof(ITestStartupJob));

        return jobs;
    }
}

public record StartupResult(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    MethodInfo TriggeredBy,
    IReadOnlyList<Type> startupJobs);