using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Ids;

public interface ISourceKnownIdUtils
{
    /// <summary>
    /// Generates Ids for the app, app instance and entity.
    /// Gets appId and appInstanceId from appsettings.
    /// Uses <see cref="IEpochTimeUtils.Epoch"/>"
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which Ids are generated. Must be a reference type.</typeparam>
    long Next<TEntity>() where TEntity : class;

    long Next<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class;

    SourceKnownId Parse(long id, DateTimeOffset? epoch = null);
}

[Singleton<ISourceKnownIdUtils>]
public class SourceKnownIdUtils(IAppSettings appSettings, IEpochTimeUtils epochTimeUtils) : ISourceKnownIdUtils
{
    public static byte MaxAppId => 127;
    public static byte MaxAppInstanceId => 63;

    public static long Generate<TEntity>(byte appId, byte appInstanceId) where TEntity : class
    {
        var builder = NumberBuilder.GetLong();

        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId();
        if (timeScopedId.TimeStamp is < 0 or > int.MaxValue)
            throw new InvalidOperationException($"Timestamp: {timeScopedId.TimeStamp} must be between 0 and {int.MaxValue}");

        //Timestamp with precision up to the second, precision more than that does not make sense
        //No one should expect resolution of an atomic clock.
        //Given measurement uncertainty because of the eventual consistency, who can claim precision finer than seconds?

        //Works for next 34 years per half since 2025 (30-bit timestamp)
        //Sign bit set to 1 (default) makes the long value negative, covering the first ~34 years of the epoch.
        //Sign bit set to 0 makes the long value positive, extending coverage for the second ~34 years.
        //Negative values sort before positive values, preserving monotonic ordering across the full ~68-year epoch.
        //todo: update sign bit for other half of the epoch
        builder.SetResidueValue((uint)timeScopedId.TimeStamp);

        //128 apps (7 bits) — sufficient for any application topology
        builder.TryAdd(appId, 7);

        //64 app instances per microservice (6 bits) — sufficient for horizontal scaling
        builder.TryAdd(appInstanceId, 6);

        //1,048,576 sequences per second (20 bits) — sufficient for high-performance scenarios
        //System-wide throughput: 8,192 generators × ~1M/s = ~8.6B IDs/s
        builder.TryAdd(timeScopedId.SequenceId, 20);

        return builder.GetValue();
    }

    private readonly byte _nexusAppId = appSettings.NexusAppSettings.AppId;
    private readonly byte _nexusAppInstanceId = appSettings.NexusAppSettings.AppInstanceId;
    private readonly DateTimeOffset _epoch = epochTimeUtils.Epoch;

    public long Next<TEntity>() where TEntity : class
        => Next<TEntity>(_nexusAppId, _nexusAppInstanceId);

    public long Next<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class
        => Generate<TEntity>(appId, appInstanceId);

    public SourceKnownId Parse(long id, DateTimeOffset? epoch = null) => ParseId(id, _epoch);

    public static SourceKnownId ParseId(long id, DateTimeOffset epoch)
    {
        var parser = NumberParser.Get(id);
        var appId = (byte)parser.Read(7);
        var appInstanceId = (byte)parser.Read(6);
        var instanceId = parser.Read(20);

        var dateTime = EpochTimeUtils.ConvertToDateTime(parser.ReadResidueValue(), epoch);
        return new SourceKnownId(id, dateTime, instanceId, appId, appInstanceId);
    }
}