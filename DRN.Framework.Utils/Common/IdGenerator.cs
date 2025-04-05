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
    /// Uses <see cref="IdGenerator.Epoch2025"/>"
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which Ids are generated. Must be a reference type.</typeparam>
    long NextId<TEntity>() where TEntity : class;

    long NextId<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class;

    IdInfo Parse(long id, DateTimeOffset? epoch = null);
}

[Scoped<ISourceKnownIdGenerator>]
public class IdGenerator(IAppSettings appSettings) : ISourceKnownIdGenerator
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

        builder.SetResidueValue((uint)timeScopedId.TimeStamp);
        builder.TryAddByte(appId);
        builder.TryAddByte(appInstanceId);
        builder.TryAddUnsignedShort(timeScopedId.SequenceId);

        return builder.GetValue();
    }

    public static IdInfo ParseId(long id, DateTimeOffset? epoch = null)
    {
        var parser = new LongParser(id, NumberBuildDirection.MostSignificantFirst, ResidueType.UInt);

        var timeStamp = parser.ReadResidueValue();
        var appId = parser.ReadByte();
        var appInstanceId = parser.ReadByte();
        var instanceId = parser.ReadUShort();
        var dateTime = (epoch ?? DefaultEpoch) + TimeSpan.FromSeconds(timeStamp);

        return new IdInfo(appId, appInstanceId, instanceId, dateTime, id);
    }


    public long NextId<TEntity>() where TEntity : class
        => GenerateId<TEntity>(appSettings.Nexus.NexusAppId, appSettings.Nexus.NexusAppInstanceId);

    public long NextId<TEntity>(byte appId, byte appInstanceId, DateTimeOffset? epoch = null) where TEntity : class
        => GenerateId<TEntity>(appId, appInstanceId, epoch);

    public IdInfo Parse(long id, DateTimeOffset? epoch = null) => ParseId(id, epoch);
}

public record struct IdInfo(byte AppId, byte AppInstanceId, ushort InstanceId, DateTimeOffset CreatedAt, long Id);