using DRN.Framework.Utils.Cancellation;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Entity;

public interface IEntityUtils
{
    ISourceKnownIdUtils Id { get; }
    ISourceKnownEntityIdUtils EntityId { get; }
    IPaginationUtils Pagination { get; }
    IEntityDateTimeUtils DateTime { get; }
    IMonotonicSystemDateTime SystemDateTime { get; }
    ICancellationUtils Cancellation { get; }
}

[Scoped<IEntityUtils>]
public class EntityUtils(
    ISourceKnownIdUtils idUtils,
    ISourceKnownEntityIdUtils entityIdUtils,
    IPaginationUtils paginationUtils,
    IEntityDateTimeUtils dateTimeUtils,
    IMonotonicSystemDateTime systemDateTimeUtils,
    ICancellationUtils cancellationUtils) : IEntityUtils
{
    public ISourceKnownIdUtils Id { get; } = idUtils;
    public ISourceKnownEntityIdUtils EntityId { get; } = entityIdUtils;
    public IPaginationUtils Pagination { get; } = paginationUtils;
    public IEntityDateTimeUtils DateTime { get; } = dateTimeUtils;
    public IMonotonicSystemDateTime SystemDateTime { get; } = systemDateTimeUtils;
    public ICancellationUtils Cancellation { get; } = cancellationUtils;
}