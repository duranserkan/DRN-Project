namespace DRN.Framework.Testing.Contexts.Startup;

public class StartupContext(ITestStartupJob job) : TestContext(GetMethodInfo(job))
{
    private static MethodInfo GetMethodInfo(ITestStartupJob job)
    {
        var methodInfo = job.GetType().GetMethod(nameof(ITestStartupJob.Run))!;

        return methodInfo;
    }
}