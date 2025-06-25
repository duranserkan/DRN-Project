using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;

namespace DRN.Framework.Utils.Entity;

public interface IEntityUtils
{
    ISourceKnownIdUtils Id { get; }
    ISourceKnownEntityIdUtils EntityId { get; }
    IPaginationUtils Pagination { get; }
    IEntityDateTimeUtils DateTime { get; }
}

[Singleton<IEntityUtils>]
public class EntityUtils(ISourceKnownIdUtils idUtils, ISourceKnownEntityIdUtils entityIdUtils, IPaginationUtils paginationUtils, IEntityDateTimeUtils dateTimeUtils) : IEntityUtils
{
    public ISourceKnownIdUtils Id { get; } = idUtils;
    public ISourceKnownEntityIdUtils EntityId { get; } = entityIdUtils;
    public IPaginationUtils Pagination { get; } = paginationUtils;
    public IEntityDateTimeUtils DateTime { get; } = dateTimeUtils;
}