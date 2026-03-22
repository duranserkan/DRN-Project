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
    public const byte MaxAppId = 127;
    public const byte MaxAppInstanceId = 63;
    public const long TicksPerHalf = 1L << 32;        // 2^32 ticks per half-epoch
    public const long MaxEpochTicks = (TicksPerHalf << 1) - 1; // 2^33 - 1: full ~68-year epoch (both halves)

    public static long Generate<TEntity>(byte appId, byte appInstanceId) where TEntity : class
    {
        var builder = NumberBuilder.GetLong();
        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId();

        //Timestamp with 250ms precision (4 ticks per second)
        //Sub-second ordering eliminates coarse-grained temporal ambiguity while preserving throughput.
        if (timeScopedId.TimeStamp is < 0 or > MaxEpochTicks)
            throw new InvalidOperationException($"Timestamp: {timeScopedId.TimeStamp} must be between 0 and {MaxEpochTicks}");

        //Epoch half determination: 32-bit timestamp, sign bit selects half
        //First half (ticks < 2^32): sign=1, negative SKID. Second half (ticks ≥ 2^32): sign=0, positive SKID.
        //Negative sorts before positive, preserving monotonic ordering across the full ~68-year epoch.
        var isSecondHalf = timeScopedId.TimeStamp >= TicksPerHalf;
        var storedTimestamp = (uint)(timeScopedId.TimeStamp & uint.MaxValue); // Mask to 32 bits
        builder.SetResidueValue(storedTimestamp);
        if (isSecondHalf)
            builder.MakePositive();

        //128 apps (7 bits) — sufficient for any application topology
        builder.TryAdd(appId, 7);

        //64 app instances per microservice (6 bits) — sufficient for horizontal scaling
        builder.TryAdd(appInstanceId, 6);

        //262,144 sequences per 250ms tick (18 bits) — sufficient for high-performance scenarios
        //Per-second throughput: 262,144 × 4 = 1,048,576 IDs/s per generator
        //System-wide throughput: 8,192 generators × ~1M/s = ~8.6B IDs/s
        builder.TryAdd(timeScopedId.SequenceId, 18);

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
        var instanceId = parser.Read(18);

        var storedTimestamp = parser.ReadResidueValue();
        var fullTicks = id >= 0 ? storedTimestamp + TicksPerHalf : storedTimestamp;
        var dateTime = EpochTimeUtils.ConvertToDateTime(fullTicks, epoch);
        return new SourceKnownId(id, dateTime, instanceId, appId, appInstanceId);
    }
}
