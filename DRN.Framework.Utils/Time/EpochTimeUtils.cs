using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;

namespace DRN.Framework.Utils.Time;

public interface IEpochTimeUtils
{
    DateTimeOffset Epoch { get; }
    DateTimeOffset ConvertToDatetime(long totalSeconds);
    long ConvertToSeconds(DateTimeOffset dateTime);
    long ConvertToSourceKnownIdTimeStamp(DateTimeOffset dateTime);
}

[Singleton<IEpochTimeUtils>]
public class EpochTimeUtils : IEpochTimeUtils
{
    /// <summary>
    /// Converts total seconds since a custom epoch to DateTimeOffset
    /// </summary>
    /// <param name="totalSeconds">Number of seconds since the epoch (can be negative)</param>
    /// <param name="epoch">Reference epoch DateTimeOffset.</param>
    public static DateTimeOffset ConvertToDateTime(long totalSeconds, DateTimeOffset epoch)
        => epoch.Add(TimeSpan.FromSeconds(totalSeconds));

    /// <summary>
    /// Calculates total seconds between a given DateTimeOffset and a specified epoch
    /// </summary>
    /// <param name="dateTime">Target DateTimeOffset</param>
    /// <param name="epoch">Reference epoch DateTimeOffset</param>
    public static long ConvertToSeconds(DateTimeOffset dateTime, DateTimeOffset epoch)
        => (long)(dateTime - epoch).TotalSeconds;

    /// <summary>
    /// Converts a DateTimeOffset to a SourceKnownId timestamp according to the application epoch
    /// </summary>
    public static long ConvertToSourceKnownIdTimeStamp(DateTimeOffset dateTime, DateTimeOffset epoch)
    {
        var builder = LongBuilder.Default;
        var duration = (uint)ConvertToSeconds(dateTime, epoch);
        builder.SetResidueValue(duration);

        return builder.GetValue();
    }
    
    //todo: make _epoch configurable at startup
    //todo: validate system time on startup
    public static readonly DateTimeOffset Epoch2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DefaultEpoch = Epoch2025;
    
    public DateTimeOffset Epoch { get; } = DefaultEpoch;

    /// <summary>
    /// Converts total seconds since the application epoch to DateTimeOffset
    /// </summary>
    /// <param name="totalSeconds">Number of seconds since the epoch (can be negative)</param>
    public DateTimeOffset ConvertToDatetime(long totalSeconds)
        => ConvertToDateTime(totalSeconds, Epoch);

    /// <summary>
    /// Calculates total seconds between a given DateTimeOffset and the application epoch
    /// </summary>
    /// <param name="dateTime">Target DateTimeOffset</param>
    public long ConvertToSeconds(DateTimeOffset dateTime)
        => ConvertToSeconds(dateTime, Epoch);

    /// <summary>
    /// Converts a DateTimeOffset to a SourceKnownId timestamp according to the application epoch
    /// </summary>
    public long ConvertToSourceKnownIdTimeStamp(DateTimeOffset dateTime)
        => ConvertToSourceKnownIdTimeStamp(dateTime, Epoch);
    //todo write IQueryable extensions to filter entity with timestamp;
}