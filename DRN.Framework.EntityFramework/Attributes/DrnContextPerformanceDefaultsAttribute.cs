using DRN.Framework.EntityFramework.Context;

namespace DRN.Framework.EntityFramework.Attributes;

/// <summary>
/// Provides <a href="https://www.npgsql.org/doc/connection-string-parameters.html">performance</a> defaults for <see cref="DrnContext{TContext}"/>. To override use a new attribute inherits from <see cref="NpgsqlPerformanceSettingsAttribute"/>.
/// </summary>
public class DrnContextPerformanceDefaultsAttribute : NpgsqlPerformanceSettingsAttribute
{
    public DrnContextPerformanceDefaultsAttribute(bool multiplexing = true,
        int maxAutoPrepare = 200,
        int autoPrepareMinUsages = 5,
        int minPoolSize = 1,
        int maxPoolSize = 15,
        int readBufferSize = 8192,
        int writeBufferSize = 8192,
        int commandTimeout = 30) : base(multiplexing, maxAutoPrepare, autoPrepareMinUsages,
        minPoolSize, maxPoolSize, readBufferSize, writeBufferSize, commandTimeout)
    {
        FrameworkDefined = true;
    }
}