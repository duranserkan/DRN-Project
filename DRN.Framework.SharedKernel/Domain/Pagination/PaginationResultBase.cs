namespace DRN.Framework.SharedKernel.Domain.Pagination;

public abstract class PaginationResultBase
{
    protected PaginationResultBase()
    {
    }

    protected PaginationResultBase(PaginationResultBase paginationResult)
    {
        Request = paginationResult.Request;
        PageNumber = paginationResult.PageNumber;
        PageSize = paginationResult.PageSize;
        FirstId = paginationResult.FirstId;
        LastId = paginationResult.LastId;
        ItemCount = paginationResult.ItemCount;
        Total = paginationResult.Total;
        HasNext = paginationResult.HasNext;
        HasPrevious = paginationResult.HasPrevious;
    }

    public PaginationRequest Request { get; protected init; }

    /// <summary>
    /// Starts from 1.
    /// </summary>
    public long PageNumber { get; protected init; }

    public int PageSize { get; protected init; }

    public Guid FirstId { get; protected init; }
    public Guid LastId { get; protected init; }
    public int ItemCount { get; protected init; }
    public PaginationTotal Total { get; protected init; }
    public bool HasNext { get; protected init; }
    public bool HasPrevious { get; protected init; }

    public long GetTotalCountUpToCurrentPage(bool includeCurrentPage = true)
        => (PageNumber - 1) * PageSize + (includeCurrentPage ? ItemCount : 0);

    public PaginationRequest RequestNextPage(bool updateTotalCount = false, long totalCount = -1)
        => Request.GetNextPage(FirstId, LastId, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);

    public PaginationRequest RequestPreviousPage(bool updateTotalCount = false, long totalCount = -1)
        => Request.GetPreviousPage(FirstId, LastId, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);

    public PaginationRequest RequestPage(long page, bool updateTotalCount = false, long totalCount = -1)
        => Request.GetPage(FirstId, LastId, PageNumber, page, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);
}