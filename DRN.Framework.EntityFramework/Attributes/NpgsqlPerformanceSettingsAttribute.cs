using Npgsql;

namespace DRN.Framework.EntityFramework.Attributes;

/// <summary>
/// https://www.npgsql.org/doc/connection-string-parameters.html
/// </summary>
public abstract class NpgsqlPerformanceSettingsAttribute(
    bool? multiplexing = null,
    int? maxAutoPrepare = null,
    int? autoPrepareMinUsages = null,
    int? minPoolSize = null,
    int? maxPoolSize = null,
    int? readBufferSize = null,
    int? writeBufferSize = null,
    int? commandTimeout = null)
    : NpgsqlDbContextOptionsAttribute
{
    private bool? Multiplexing { get; } = multiplexing;
    private int? MaxAutoPrepare { get; } = maxAutoPrepare;
    private int? AutoPrepareMinUsages { get; } = autoPrepareMinUsages;
    private int? MinPoolSize { get; } = minPoolSize;
    private int? MaxPoolSize { get; } = maxPoolSize;
    private int? ReadBufferSize { get; } = readBufferSize;
    private int? WriteBufferSize { get; } = writeBufferSize;
    private int? CommandTimeout { get; } = commandTimeout;

    public override void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider? serviceProvider)
    {
        var csBuilder = builder.ConnectionStringBuilder;

        if (Multiplexing != null) csBuilder.Multiplexing = Multiplexing.Value;
        if (MaxAutoPrepare != null) csBuilder.MaxAutoPrepare = MaxAutoPrepare.Value;
        if (AutoPrepareMinUsages != null) csBuilder.AutoPrepareMinUsages = AutoPrepareMinUsages.Value;
        if (MinPoolSize != null) csBuilder.MinPoolSize = MinPoolSize.Value;
        if (MaxPoolSize != null) csBuilder.MaxPoolSize = MaxPoolSize.Value;
        if (ReadBufferSize != null) csBuilder.ReadBufferSize = ReadBufferSize.Value;
        if (WriteBufferSize != null) csBuilder.WriteBufferSize = WriteBufferSize.Value;
        if (CommandTimeout != null) csBuilder.CommandTimeout = CommandTimeout.Value;
    }
}