using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;

namespace DRN.Framework.Utils.Time;

public interface IEpochTimeUtils
{
    DateTimeOffset Epoch { get; }
    DateTimeOffset ConvertToDatetime(long totalTicks);
    long ConvertToTicks(DateTimeOffset dateTime);
    long ConvertToSourceKnownIdTimeStamp(DateTimeOffset dateTime);
}

[Singleton<IEpochTimeUtils>]
public class EpochTimeUtils : IEpochTimeUtils
{
    /// <summary>
    /// Converts total 250ms ticks since the configured process epoch to DateTimeOffset.
    /// </summary>
    /// <param name="totalTicks">Number of 250ms ticks since the epoch (can be negative)</param>
    public static DateTimeOffset ConvertToDateTime(long totalTicks) => ConvertToDateTime(totalTicks, DefaultEpoch);

    internal static DateTimeOffset ConvertToDateTime(long totalTicks, DateTimeOffset epoch)
        => epoch.Add(TimeSpan.FromTicks(checked(totalTicks * TimeStampManager.TicksPerPrecisionUnit)));

    /// <summary>
    /// Calculates total 250ms ticks between a given DateTimeOffset and a specified epoch
    /// </summary>
    /// <param name="dateTime">Target DateTimeOffset</param>
    /// <param name="epoch">Reference epoch DateTimeOffset</param>
    internal static long ConvertToTicks(DateTimeOffset dateTime, DateTimeOffset epoch)
        => (dateTime - epoch).Ticks / TimeStampManager.TicksPerPrecisionUnit;

    /// <summary>
    /// Converts a DateTimeOffset to a SourceKnownId timestamp according to the application epoch
    /// </summary>
    internal static long ConvertToSourceKnownIdTimeStamp(DateTimeOffset dateTime, DateTimeOffset epoch)
    {
        if (dateTime < epoch)
            throw new InvalidOperationException($"UTC {dateTime:O} is before epoch origin {epoch:O}.");
        var elapsedTicks = ConvertToTicks(dateTime, epoch);
        if (elapsedTicks is < 0 or > SourceKnownIdUtils.MaxEpochTicks)
            throw new InvalidOperationException($"Elapsed ticks: {elapsedTicks} must be between 0 and {SourceKnownIdUtils.MaxEpochTicks}");

        var builder = NumberBuilder.GetLong();
        var storedTimestamp = (uint)(elapsedTicks & uint.MaxValue); // Mask to 32 bits
        builder.SetResidueValue(storedTimestamp);
        if (elapsedTicks >= SourceKnownIdUtils.TicksPerHalf) // Second epoch half → positive SKID
            builder.MakePositive();

        return builder.GetValue();
    }
    
    // Encoded GUID epoch 0 uses one immutable configured origin throughout the process.
    public static readonly DateTimeOffset Epoch2025 = SourceKnownGenerationTimePolicy.DefaultEpoch;
    public static DateTimeOffset DefaultEpoch => SourceKnownGenerationTime.Epoch;
    
    public DateTimeOffset Epoch => DefaultEpoch;

    /// <summary>
    /// Converts total 250ms ticks since the application epoch to DateTimeOffset
    /// </summary>
    /// <param name="totalTicks">Number of 250ms ticks since the epoch (can be negative)</param>
    public DateTimeOffset ConvertToDatetime(long totalTicks) => ConvertToDateTime(totalTicks, Epoch);

    /// <summary>
    /// Calculates total 250ms ticks between a given DateTimeOffset and the application epoch
    /// </summary>
    /// <param name="dateTime">Target DateTimeOffset</param>
    public long ConvertToTicks(DateTimeOffset dateTime) => ConvertToTicks(dateTime, Epoch);

    /// <summary>
    /// Converts a DateTimeOffset to a SourceKnownId timestamp according to the application epoch
    /// </summary>
    public long ConvertToSourceKnownIdTimeStamp(DateTimeOffset dateTime) => ConvertToSourceKnownIdTimeStamp(dateTime, Epoch);
}
