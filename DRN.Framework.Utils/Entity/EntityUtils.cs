using DRN.Framework.SharedKernel.Utils;
using DRN.Framework.Utils.Cancellation;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;

namespace DRN.Framework.Utils.Entity;

public interface IEntityUtils
{
    ISourceKnownIdUtils Id { get; }
    ISourceKnownEntityIdUtils EntityId { get; }
    ICancellationUtils Cancellation { get; }
    IPaginationUtils Pagination { get; }
    IEntityDateTimeUtils DateTime { get; }
    DateTimeOffset UtcNow { get; }
}

[Scoped<IEntityUtils>]
public class EntityUtils(
    ISourceKnownIdUtils idUtils,
    ISourceKnownEntityIdUtils entityIdUtils,
    ICancellationUtils cancellationUtils,
    IPaginationUtils paginationUtils,
    IEntityDateTimeUtils dateTimeUtils,
    IDateTimeProvider dateTimeProvider) : IEntityUtils
{
    public ISourceKnownIdUtils Id { get; } = idUtils; 
    public ISourceKnownEntityIdUtils EntityId { get; } = entityIdUtils;
    public ICancellationUtils Cancellation { get; } = cancellationUtils;
    public IPaginationUtils Pagination { get; } = paginationUtils;
    public IEntityDateTimeUtils DateTime { get; } = dateTimeUtils;
    public DateTimeOffset UtcNow { get; } = dateTimeProvider.UtcNow;
}