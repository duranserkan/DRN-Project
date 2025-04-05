using DRN.Framework.Utils.Common.Numbers;
using DRN.Framework.Utils.Common.Sequences;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Common;

//todo: add guid version with entityTypeId and mac parts
public interface ISourceKnownIdGenerator
{
    /// <summary>
    /// Generates Ids for the app, app instance and entity.
    /// Gets appId and appInstanceId from appsettings.
    /// Uses <see cref="SourceKnownIdGenerator.Epoch2025"/>"
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which Ids are generated. Must be a reference type.</typeparam>
    long NextId<TEntity>() where TEntity : class;

    long NextId<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class;

    SourceKnownIdInfo Parse(long id, DateTimeOffset? epoch = null);
}

[Scoped<ISourceKnownIdGenerator>]
public class SourceKnownIdGenerator(IAppSettings appSettings) : ISourceKnownIdGenerator
{
    //todo: make _epoch configurable at startup
    //todo: validate system time on startup
    public static readonly DateTimeOffset Epoch2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    internal static DateTimeOffset DefaultEpoch = Epoch2025;

    public static long GenerateId<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class
    {
        var builder = LongBuilder.Default;

        var timeScopedId = SequenceManager<TEntity>.GetTimeScopedId();
        if (timeScopedId.TimeStamp is < 0 or > int.MaxValue)
            throw new InvalidOperationException($"Timestamp: {timeScopedId.TimeStamp} must be between 0 and {int.MaxValue}");

        //timestamp with precision up to the second, a precision more than that does not make sense
        //No one should expect resolution of an atomic clock.
        //Given measurement uncertainty because of the eventual consistency, who can claim a precision finer than seconds?

        //Works for next 68 years since 2025
        //Sign-bit is by default positive that makes the long value negative
        //When additional interval needed sign-bit should be 0
        //That makes generated value, positive keeps ordering and provides another 68 year
        builder.SetResidueValue((uint)timeScopedId.TimeStamp);

        //Initially 256 apps were allowed.
        //However, that many microservices are hard to maintain and sign of bad design
        //64 considered to be safe for any application
        builder.TryAdd(appId, 6);

        //Initially  256 apps instances per microservice wer allowed.
        //However, that many instances may create unnecessary connections and create performance problems at elsewhere
        //10-20 app instance should be sufficient for horizontal scaling
        builder.TryAdd(appInstanceId, 5);

        //Initially max sequence length set as 65536 (16bit) however it may not be enough for high performance scenarios
        //Since spared 5 bit from appId and appInstanceId we can invest them into sequenceId to support 2097152 long sequence per second
        //It can generate full positive int range less than 20 minutes
        builder.TryAdd(timeScopedId.SequenceId, 21);

        return builder.GetValue();
    }

    public static SourceKnownIdInfo ParseId(long id, DateTimeOffset? epoch = null)
    {
        var parser = LongParser.Default(id);

        var timeStamp = parser.ReadResidueValue();
        var appId = parser.Read(6);
        var appInstanceId = parser.Read(5);
        var instanceId = parser.Read(21);
        var dateTime = (epoch ?? DefaultEpoch) + TimeSpan.FromSeconds(timeStamp);

        return new SourceKnownIdInfo((byte)appId, (byte)appInstanceId, instanceId, dateTime, id);
    }

    public long NextId<TEntity>() where TEntity : class
        => GenerateId<TEntity>(appSettings.Nexus.NexusAppId, appSettings.Nexus.NexusAppInstanceId);

    public long NextId<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class
        => GenerateId<TEntity>(appId, appInstanceId, epoch);

    public SourceKnownIdInfo Parse(long id, DateTimeOffset? epoch = null) => ParseId(id, epoch);
}

public record struct SourceKnownIdInfo(byte AppId, byte AppInstanceId, uint InstanceId, DateTimeOffset CreatedAt, long Id);