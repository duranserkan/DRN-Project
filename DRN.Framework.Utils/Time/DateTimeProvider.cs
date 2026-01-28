using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Time;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public interface ISystemDateTimeProvider : IDateTimeProvider;

[Singleton<IDateTimeProvider>]
public class SystemDateTimeProvider : ISystemDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public static class DateTimeProvider
{
    public static DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}