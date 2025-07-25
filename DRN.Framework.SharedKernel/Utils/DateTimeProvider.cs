namespace DRN.Framework.SharedKernel.Utils;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface ISystemDateTimeProvider : IDateTimeProvider;

internal class SystemDateTime : ISystemDateTimeProvider;

public static class DateTimeProvider
{
    internal static IDateTimeProvider Provider = new SystemDateTime();

    public static DateTimeOffset UtcNow => Provider.UtcNow;
}