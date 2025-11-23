using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Npgsql;

namespace DRN.Framework.Testing.Contexts.Startup;

public static class StartupJobRunner
{
    static StartupJobRunner() => UtilsConventionBuilder.BuildConvention();

    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);

    public static TestStartupResult Result { get; private set; } = null!;

    public static void TriggerStartupJobs(MethodInfo testMethod, Type type)
    {
        if (_triggered || type != typeof(DrnTestContext)) return;

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
        TestEnvironment.DrnTestContextEnabled = true;
        var startedAt = DateTimeOffset.Now;
        var jobTypes = GetTestStartupJobTypes(testMethod);

        try
        {
            foreach (var startupJobType in jobTypes)
            {
                var startupJob = (ITestStartupJob)Activator.CreateInstance(startupJobType)!;
                using var startupContext = new StartupContext(startupJob);
                startupJob.RunAsync(startupContext).GetAwaiter().GetResult();
            }
        }
        catch (PostgresException ex)
        {
            //Ignored Npgsql.PostgresException : 42P01: relation "entity_migrations.{context}_history" does not exist
            _ = ex;
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