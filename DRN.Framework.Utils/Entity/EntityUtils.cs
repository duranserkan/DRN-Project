using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;

namespace DRN.Framework.Utils.Entity;

public interface IEntityUtils
{
    ISourceKnownIdUtils IdUtils { get; }
    ISourceKnownEntityIdUtils EntityIdUtils { get; }
    IPaginationUtils PaginationUtils { get; }
    IEntityDateTimeUtils DateTimeUtils { get; }
}

[Singleton<IEntityUtils>]
public class EntityUtils(ISourceKnownIdUtils idUtils, ISourceKnownEntityIdUtils entityIdUtils, IPaginationUtils paginationUtils, IEntityDateTimeUtils dateTimeUtils) : IEntityUtils
{
    public ISourceKnownIdUtils IdUtils { get; } = idUtils;
    public ISourceKnownEntityIdUtils EntityIdUtils { get; } = entityIdUtils;
    public IPaginationUtils PaginationUtils { get; } = paginationUtils;
    public IEntityDateTimeUtils DateTimeUtils { get; } = dateTimeUtils;
}