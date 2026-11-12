using PageSizeDefaults = DRN.Framework.SharedKernel.Domain.Pagination.PageSize;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>Flat options for the first page of a pagination request. The total is unspecified until counted by the server.</summary>
public sealed class PaginationOptions
{
    public int PageSize { get; set; } = PageSizeDefaults.SizeDefault;
    public int MaxSize { get; set; } = PageSizeDefaults.MaxSizeDefault;
    /// <summary>Requests a server-side count of matching items.</summary>
    public bool UpdateTotalCount { get; set; }
    public PageSortDirection Direction { get; set; } = PageSortDirection.Ascending;
}
