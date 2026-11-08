using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.Testing.Contexts.Startup;

public class StartupContext(ITestStartupJob job) : DrnTestContext(GetMethodInfo(job))
{
    private static MethodInfo GetMethodInfo(ITestStartupJob job) => job.GetType().GetMethod(nameof(ITestStartupJob.RunAsync))!;

    [SuppressMessage("SonarQube", "S2325", Justification = "Startup jobs create child test contexts through the StartupContext instance supplied to RunAsync.")]
    [SuppressMessage("Performance", "CA1822", Justification = "Making this public instance method static would break existing startup job callers.")]
    public DrnTestContext CreateNewContext(MethodInfo methodInfo) => new(methodInfo, false);
}
