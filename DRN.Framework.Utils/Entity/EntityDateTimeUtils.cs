using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Entity;

public interface IEntityDateTimeUtils
{
    long ConvertToTimeStamp(DateTimeOffset dateTimeOffset);

    IQueryable<TEntity> CreatedAfter<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedBefore<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateAfterCreation, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedBetween<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, DateTimeOffset dateAfterCreation, bool inclusive = true)
        where TEntity : SourceKnownEntity;
}

[Singleton<IEntityDateTimeUtils>]
public class EntityDateTimeUtils : IEntityDateTimeUtils
{
    public long ConvertToTimeStamp(DateTimeOffset dateTimeOffset)
        => EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(dateTimeOffset, EpochTimeUtils.DefaultEpoch);

    public IQueryable<TEntity> CreatedAfter<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation,
        bool inclusive = true) where TEntity : SourceKnownEntity
        => inclusive
            ? query.Where(entity => entity.Id >= ConvertToTimeStamp(dateBeforeCreation))
            : query.Where(e => e.Id > ConvertToTimeStamp(dateBeforeCreation));

    public IQueryable<TEntity> CreatedBefore<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateAfterCreation,
        bool inclusive = true) where TEntity : SourceKnownEntity
        => inclusive
            ? query.Where(entity => entity.Id <= ConvertToTimeStamp(dateAfterCreation))
            : query.Where(entity => entity.Id < ConvertToTimeStamp(dateAfterCreation));

    public IQueryable<TEntity> CreatedBetween<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, DateTimeOffset dateAfterCreation,
        bool inclusive = true)
        where TEntity : SourceKnownEntity
    {
        var timestampAfterCreation = ConvertToTimeStamp(dateAfterCreation);
        var timestampBeforeCreation = ConvertToTimeStamp(dateBeforeCreation);

        return inclusive
            ? query.Where(entity => entity.Id <= timestampAfterCreation && entity.Id >= timestampBeforeCreation)
            : query.Where(entity => entity.Id < timestampAfterCreation && entity.Id > timestampBeforeCreation);
    }
}