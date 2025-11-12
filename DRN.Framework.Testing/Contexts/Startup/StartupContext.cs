namespace DRN.Framework.Testing.Contexts.Startup;

public class StartupContext(ITestStartupJob job) : DrnTestContext(GetMethodInfo(job))
{
    private static MethodInfo GetMethodInfo(ITestStartupJob job) => job.GetType().GetMethod(nameof(ITestStartupJob.RunAsync))!;

    public DrnTestContext CreateNewContext(MethodInfo methodInfo) => new(methodInfo, false);
}