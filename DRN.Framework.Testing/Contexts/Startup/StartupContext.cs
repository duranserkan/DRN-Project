namespace DRN.Framework.Testing.Contexts.Startup;

public class StartupContext(ITestStartupJob job) : TestContext(GetMethodInfo(job))
{
    private static MethodInfo GetMethodInfo(ITestStartupJob job) => job.GetType().GetMethod(nameof(ITestStartupJob.RunAsync))!;

    public TestContext CreateNewContext(MethodInfo methodInfo) => new(methodInfo, false);
}