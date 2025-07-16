using DRN.Framework.SharedKernel.Domain;
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

    IQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> query, EntityCreated filter)
        where TEntity : SourceKnownEntity;
}

[Singleton<IEntityDateTimeUtils>]
public class EntityDateTimeUtils : IEntityDateTimeUtils
{
    private long ConvertToTimeStamp(DateTimeOffset dateTimeOffset) => EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(dateTimeOffset, EpochTimeUtils.DefaultEpoch);

    public IQueryable<TEntity> CreatedAfter<TEntity>(IQueryable<TEntity> query, DateTimeOffset date, bool inclusive = true)
        where TEntity : SourceKnownEntity
        => inclusive
            ? query.Where(entity => entity.Id >= ConvertToTimeStamp(date))
            : query.Where(e => e.Id > ConvertToTimeStamp(date));

    public IQueryable<TEntity> CreatedBefore<TEntity>(IQueryable<TEntity> query, DateTimeOffset date, bool inclusive = true)
        where TEntity : SourceKnownEntity
        => inclusive
            ? query.Where(entity => entity.Id <= ConvertToTimeStamp(date))
            : query.Where(entity => entity.Id < ConvertToTimeStamp(date));

    public IQueryable<TEntity> CreatedBetween<TEntity>(IQueryable<TEntity> query, DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var timestampAfterCreation = ConvertToTimeStamp(end);
        var timestampBeforeCreation = ConvertToTimeStamp(begin);

        //value mismatch can be fixed, no need to throw exception or return invalid results
        if (timestampBeforeCreation > timestampAfterCreation) //todo test correction
            (timestampAfterCreation, timestampBeforeCreation) = (timestampBeforeCreation, timestampAfterCreation);

        return inclusive
            ? query.Where(entity => entity.Id <= timestampAfterCreation && entity.Id >= timestampBeforeCreation)
            : query.Where(entity => entity.Id < timestampAfterCreation && entity.Id > timestampBeforeCreation);
    }

    public IQueryable<TEntity> CreatedOutside<TEntity>(IQueryable<TEntity> query, DateTimeOffset begin, DateTimeOffset end, bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var beforeTimeStamp = ConvertToTimeStamp(begin);
        var afterTimeStamp = ConvertToTimeStamp(end);

        //value mismatch can be fixed, no need to throw exception or return invalid results
        if (beforeTimeStamp > afterTimeStamp) //todo test correction
            (afterTimeStamp, beforeTimeStamp) = (beforeTimeStamp, afterTimeStamp);

        return inclusive
            ? query.Where(entity => entity.Id <= beforeTimeStamp || entity.Id >= afterTimeStamp)
            : query.Where(entity => entity.Id < beforeTimeStamp || entity.Id > afterTimeStamp);
    }

    //todo add tests
    public IQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> query, EntityCreated filter)
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