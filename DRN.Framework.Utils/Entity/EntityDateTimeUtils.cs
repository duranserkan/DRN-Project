using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Entity;

public interface IEntityDateTimeUtils
{
    IQueryable<TEntity> CreatedAfter<TEntity>(IQueryable<TEntity> query, DateTimeOffset date, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedBefore<TEntity>(IQueryable<TEntity> query, DateTimeOffset date, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedBetween<TEntity>(IQueryable<TEntity> query, DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedOutside<TEntity>(IQueryable<TEntity> query, DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> query, EntityCreatedFilter filter)
        where TEntity : SourceKnownEntity;
}

[Singleton<IEntityDateTimeUtils>]
public class EntityDateTimeUtils : IEntityDateTimeUtils
{
    private const long SourceKnownIdPayloadMask = 0x7FFF_FFFF; // 7 app + 6 app-instance + 18 sequence bits

    private static (long Min, long Max) ConvertToTickBounds(DateTimeOffset dateTimeOffset)
    {
        var min = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(dateTimeOffset, EpochTimeUtils.DefaultEpoch);
        return (min, min | SourceKnownIdPayloadMask);
    }

    public IQueryable<TEntity> CreatedAfter<TEntity>(IQueryable<TEntity> query, DateTimeOffset date, bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var bounds = ConvertToTickBounds(date);
        var threshold = inclusive ? bounds.Min : bounds.Max;

        return inclusive
            ? query.Where(entity => entity.Id >= threshold)
            : query.Where(entity => entity.Id > threshold);
    }

    public IQueryable<TEntity> CreatedBefore<TEntity>(IQueryable<TEntity> query, DateTimeOffset date, bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var bounds = ConvertToTickBounds(date);
        var threshold = inclusive ? bounds.Max : bounds.Min;

        return inclusive
            ? query.Where(entity => entity.Id <= threshold)
            : query.Where(entity => entity.Id < threshold);
    }

    public IQueryable<TEntity> CreatedBetween<TEntity>(IQueryable<TEntity> query, DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var beginBounds = ConvertToTickBounds(begin);
        var endBounds = ConvertToTickBounds(end);

        // Normalize reversed bounds instead of returning invalid results.
        if (beginBounds.Min > endBounds.Min)
            (beginBounds, endBounds) = (endBounds, beginBounds);

        var lowerThreshold = inclusive ? beginBounds.Min : beginBounds.Max;
        var upperThreshold = inclusive ? endBounds.Max : endBounds.Min;

        return inclusive
            ? query.Where(entity => entity.Id >= lowerThreshold && entity.Id <= upperThreshold)
            : query.Where(entity => entity.Id > lowerThreshold && entity.Id < upperThreshold);
    }

    public IQueryable<TEntity> CreatedOutside<TEntity>(IQueryable<TEntity> query, DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var beginBounds = ConvertToTickBounds(begin);
        var endBounds = ConvertToTickBounds(end);

        // Normalize reversed bounds instead of returning invalid results.
        if (beginBounds.Min > endBounds.Min)
            (beginBounds, endBounds) = (endBounds, beginBounds);

        var lowerThreshold = inclusive ? beginBounds.Max : beginBounds.Min;
        var upperThreshold = inclusive ? endBounds.Min : endBounds.Max;

        return inclusive
            ? query.Where(entity => entity.Id <= lowerThreshold || entity.Id >= upperThreshold)
            : query.Where(entity => entity.Id < lowerThreshold || entity.Id > upperThreshold);
    }
    
    public IQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> query, EntityCreatedFilter filter)
        where TEntity : SourceKnownEntity => filter.Type switch
    {
        DateTimeFilterType.After => CreatedAfter(query, filter.Begin, filter.Inclusive),
        DateTimeFilterType.Before => CreatedBefore(query, filter.Begin, filter.Inclusive),
        DateTimeFilterType.Between => filter.End.HasValue
            ? CreatedBetween(query, filter.Begin, filter.End.Value, filter.Inclusive)
            : throw new ArgumentException("End date is required for Between filter", nameof(filter)),
        DateTimeFilterType.Outside => filter.End.HasValue
            ? CreatedOutside(query, filter.Begin, filter.End.Value, filter.Inclusive)
            : throw new ArgumentException("End date is required for Outside filter", nameof(filter)),
        _ => throw new ArgumentOutOfRangeException(nameof(filter), "Invalid filter type")
    };
}
