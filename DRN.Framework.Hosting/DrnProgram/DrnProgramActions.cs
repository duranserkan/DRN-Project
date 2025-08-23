using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Builder;

namespace DRN.Framework.Hosting.DrnProgram;

//todo: add tests
/// <summary>
/// Implementing class must have a public parameterless constructor. 
/// Classes without parameterless constructors will be skipped during initialization.
/// Only one implementation of this interface is allowed in the assembly.
/// Multiple implementations will cause the application startup to fail.
/// </summary>
public abstract class DrnProgramActions
{
    /// <summary>
    /// Method invoked after the application builder has been created.
    /// Allows for customization and configuration of the application builder.
    /// Can be used to configure external dependencies.
    /// </summary>
    public virtual async Task ApplicationBuilderCreatedAsync<TProgram>(
        TProgram program, WebApplicationBuilder builder,
        IAppSettings appSettings, IScopedLog scopedLog)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
        => await Task.CompletedTask;

    /// <summary>
    /// Method invoked after the web application has been built.
    /// Provides a hook for further configuration and setup of the application.
    /// Can be used to finalize and validate specific application settings or dependencies.
    /// </summary>
    public virtual async Task ApplicationBuiltAsync<TProgram>(
        TProgram program, WebApplication application, IAppSettings appSettings, IScopedLog scopedLog)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
        => await Task.CompletedTask;

    /// <summary>
    /// Method invoked after the application has been validated.
    /// Provides an opportunity to perform additional validation or initialization tasks.
    /// Can be used to seed application data
    /// </summary>
    public virtual async Task ApplicationValidatedAsync<TProgram>(
        TProgram program, WebApplication application, IAppSettings appSettings, IScopedLog scopedLog)
        where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
        => await Task.CompletedTask;
}