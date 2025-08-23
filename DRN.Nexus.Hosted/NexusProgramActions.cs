#if DEBUG
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Nexus.Hosted;

/// <summary>
/// Implementing class must have a public parameterless constructor. 
/// Classes without parameterless constructors will be skipped during initialization.
/// Only one implementation of this interface is allowed in the assembly.
/// Multiple implementations will cause the application startup to fail.
/// </summary>
public class NexusProgramActions : DrnProgramActions
{
    public override async Task ApplicationBuilderCreatedAsync<TProgram>(
        TProgram program, WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
    {
        var launchOptions = new ExternalDependencyLaunchOptions
        {
            PostgresContainerSettings = new PostgresContainerSettings
            {
                Reuse = true,
                HostPort = 7432 //to keep the default port free for other usages
            }
        };
        await builder.LaunchExternalDependenciesAsync(scopedLog, appSettings, launchOptions);
    }
}
#endif