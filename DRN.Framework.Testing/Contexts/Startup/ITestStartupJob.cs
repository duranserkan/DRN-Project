namespace DRN.Framework.Testing.Contexts.Startup;

/// <summary>
/// Test Startup Job implementations should have a parameterless constructor
/// </summary>
public interface ITestStartupJob
{
    public Task RunAsync(StartupContext context);
}