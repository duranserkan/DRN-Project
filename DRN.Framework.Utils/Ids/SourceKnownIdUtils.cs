using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Ids;

public interface ISourceKnownIdUtils
{
    /// <summary>
    /// Generates Ids for the app, app instance and entity.
    /// Gets appId and appInstanceId from appsettings.
    /// Uses <see cref="SourceKnownIdUtils.Epoch2025"/>"
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which Ids are generated. Must be a reference type.</typeparam>
    long Next<TEntity>() where TEntity : class;

    long Next<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class;

    SourceKnownId Parse(long id, DateTimeOffset? epoch = null);
}

[Singleton<ISourceKnownIdUtils>]
public class SourceKnownIdUtils(IAppSettings appSettings) : ISourceKnownIdUtils
{
    //todo: make _epoch configurable at startup
    //todo: validate system time on startup
    public static readonly DateTimeOffset Epoch2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    internal static readonly DateTimeOffset DefaultEpoch = Epoch2025;
    public static byte MaxAppId => 63;
    public static byte MaxAppInstanceId => 31;

    public static long Generate<TEntity>(byte appId, byte appInstanceId) where TEntity : class
    {
        var builder = LongBuilder.Default;

        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId();
        if (timeScopedId.TimeStamp is < 0 or > int.MaxValue)
            throw new InvalidOperationException($"Timestamp: {timeScopedId.TimeStamp} must be between 0 and {int.MaxValue}");

        //Timestamp with precision up to the second, precision more than that does not make sense
        //No one should expect resolution of an atomic clock.
        //Given measurement uncertainty because of the eventual consistency, who can claim precision finer than seconds?

        //Works for next 68 years since 2025
        //Sign-bit is by default positive that makes the long value negative
        //When an additional interval needed sign-bit should be 0
        //That makes generated value, positive keeps ordering and provides another 68 years.
        builder.SetResidueValue((uint)timeScopedId.TimeStamp);

        //Initially 256 apps were allowed.
        //However, that many microservices are hard to maintain and sign of bad design
        //64 considered to be safe for any application
        builder.TryAdd(appId, 6);

        //Initially, 256 app instances per microservice were allowed.
        //However, that many instances may create unnecessary connections and create performance problems at elsewhere
        //10-20 app instances should be sufficient for horizontal scaling
        builder.TryAdd(appInstanceId, 5);

        //Initially max sequence length set as 65,536 (16bit) however, it may not be enough for high-performance scenarios
        //Since spared 5 bit from appId and appInstanceId we can invest them into sequenceId to support 2,097,152 long sequences per second
        //It can generate full positive integer range in less than 20 minutes
        builder.TryAdd(timeScopedId.SequenceId, 21);

        return builder.GetValue();
    }

    private readonly byte _nexusAppId = appSettings.NexusAppSettings.AppId;
    private readonly byte _nexusAppInstanceId = appSettings.NexusAppSettings.AppInstanceId;

    public long Next<TEntity>() where TEntity : class
        => Next<TEntity>(_nexusAppId, _nexusAppInstanceId);

    public long Next<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class 
        => Generate<TEntity>(appId, appInstanceId);

    public SourceKnownId Parse(long id, DateTimeOffset? epoch = null) => ParseId(id, epoch);

    public static SourceKnownId ParseId(long id, DateTimeOffset? epoch = null)
    {
        var parser = LongParser.Default(id);

        var appId = (byte)parser.Read(6);
        var appInstanceId = (byte)parser.Read(5);
        var instanceId = parser.Read(21);
        var dateTime = (epoch ?? DefaultEpoch) + TimeSpan.FromSeconds(parser.ReadResidueValue());

        return new SourceKnownId(id, appId, appInstanceId, instanceId, dateTime);
    }
}