using DRN.Framework.Utils.Common.Numbers;

namespace DRN.Framework.Utils.Common;

//todo:timestamp, appname,appinstance, entityname, entityinstance parts
public static class IdGenerator
{
    public static readonly DateTimeOffset Epoch2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static long GenerateId(byte appId, byte appInstanceId, DateTimeOffset? epoch = null)
    {
        var builder = LongBuilder.Default;

        var timestamp = (uint)CurrentTimestamp(epoch);
        builder.SetResidueValue(timestamp);
        builder.TryAddByte(appId);
        builder.TryAddByte(appInstanceId);
        builder.TryAddUnsignedShort(GetInstanceId());

        return builder.GetValue();
    }

    public static IdInfo ParseId(long id, DateTimeOffset? epoch = null)
    {
        var parser = new LongParser(id, NumberBuildDirection.MostSignificantFirst, ResidueType.UInt);

        var timeStamp = parser.ReadResidueValue();
        var appId = parser.ReadByte();
        var appInstanceId = parser.ReadByte();
        var instanceId = parser.ReadUShort();
        var dateTime = (epoch ?? Epoch2025) + TimeSpan.FromSeconds(timeStamp);

        return new IdInfo(appId, appInstanceId, instanceId, dateTime, id);
    }

    //todo: get instance ids from internal sequence
    private static ushort GetInstanceId() => ushort.MaxValue;

    private static int CurrentTimestamp(DateTimeOffset? epoch = null) => (int)((DateTimeOffset.UtcNow - (epoch ?? Epoch2025)).Ticks / TimeSpan.TicksPerSecond);
}

public record struct IdInfo(byte AppId, byte AppInstanceId, ushort instanceId, DateTimeOffset CreatedAt, long Id);