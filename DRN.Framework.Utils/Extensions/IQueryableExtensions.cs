using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Extensions;

public static class IQueryableExtensions
{
    private static long ConvertToTimeStamp(DateTimeOffset dateTimeOffset)
        => EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(dateTimeOffset, EpochTimeUtils.DefaultEpoch);

    public static IQueryable<TEntity> CreatedAfter<TEntity>(this IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, 
        bool inclusive = true) where TEntity : Entity
        => inclusive
            ? query.Where(entity => entity.Id >= ConvertToTimeStamp(dateBeforeCreation))
            : query.Where(e => e.Id > ConvertToTimeStamp(dateBeforeCreation));

    public static IQueryable<TEntity> CreatedBefore<TEntity>(this IQueryable<TEntity> query, DateTimeOffset dateAfterCreation,
        bool inclusive = true) where TEntity : Entity
        => inclusive
            ? query.Where(entity => entity.Id <= ConvertToTimeStamp(dateAfterCreation))
            : query.Where(entity => entity.Id < ConvertToTimeStamp(dateAfterCreation));

    public static IQueryable<TEntity> CreatedBetween<TEntity>(this IQueryable<TEntity> query, DateTimeOffset dateBeforeCreation, DateTimeOffset dateAfterCreation,
        bool inclusive = true)
        where TEntity : Entity
    {
        var timestampAfterCreation = ConvertToTimeStamp(dateAfterCreation);
        var timestampBeforeCreation = ConvertToTimeStamp(dateBeforeCreation);

        return inclusive
            ? query.Where(entity => entity.Id <= timestampAfterCreation && entity.Id >= timestampBeforeCreation)
            : query.Where(entity => entity.Id < timestampAfterCreation && entity.Id > timestampBeforeCreation);
    }
}