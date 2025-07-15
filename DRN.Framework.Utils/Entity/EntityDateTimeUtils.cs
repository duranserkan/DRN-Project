using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Entity;

public interface IEntityDateTimeUtils
{
    IQueryable<TEntity> CreatedAfter<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedBefore<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateAfterCreation, bool inclusive = true)
        where TEntity : SourceKnownEntity;

    IQueryable<TEntity> CreatedBetween<TEntity>(IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, DateTimeOffset dateAfterCreation, bool inclusive = true)
        where TEntity : SourceKnownEntity;
    
    IQueryable<TEntity> CreatedOutside<TEntity>(IQueryable<TEntity> query, DateTimeOffset before, DateTimeOffset after, bool inclusive = true)
        where TEntity : SourceKnownEntity;
}

[Singleton<IEntityDateTimeUtils>]
public class EntityDateTimeUtils : IEntityDateTimeUtils
{
    private long ConvertToTimeStamp(DateTimeOffset dateTimeOffset)
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

        //value mismatch can be fixed, no need to throw exception or return invalid results
        if (timestampBeforeCreation > timestampAfterCreation) //todo test correction
            (timestampAfterCreation, timestampBeforeCreation) = (timestampBeforeCreation, timestampAfterCreation);

        return inclusive
            ? query.Where(entity => entity.Id <= timestampAfterCreation && entity.Id >= timestampBeforeCreation)
            : query.Where(entity => entity.Id < timestampAfterCreation && entity.Id > timestampBeforeCreation);
    }

    public IQueryable<TEntity> CreatedOutside<TEntity>(IQueryable<TEntity> query, DateTimeOffset before, DateTimeOffset after, bool inclusive = true) where TEntity : SourceKnownEntity
    {
        var beforeTimeStamp = ConvertToTimeStamp(before);
        var afterTimeStamp = ConvertToTimeStamp(after);
        
        //value mismatch can be fixed, no need to throw exception or return invalid results
        if (beforeTimeStamp > afterTimeStamp) //todo test correction
            (afterTimeStamp, beforeTimeStamp) = (beforeTimeStamp, afterTimeStamp);
        
        return inclusive
            ? query.Where(entity => entity.Id <= beforeTimeStamp && entity.Id >= afterTimeStamp)
            : query.Where(entity => entity.Id < beforeTimeStamp && entity.Id > afterTimeStamp);
    }
}