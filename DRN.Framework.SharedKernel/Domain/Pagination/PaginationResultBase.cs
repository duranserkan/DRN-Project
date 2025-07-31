using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public abstract class PaginationResultBase
{
    protected PaginationResultBase() => Request = null!;

    protected PaginationResultBase(PaginationResultBase paginationResult)
    {
        Request = paginationResult.Request;
        PageNumber = paginationResult.PageNumber;
        PageSize = paginationResult.PageSize;
        FirstId = paginationResult.FirstId;
        LastId = paginationResult.LastId;
        ItemCount = paginationResult.ItemCount;
        HasNext = paginationResult.HasNext;
        HasPrevious = paginationResult.HasPrevious;
        Total = paginationResult.Total;
        TotalCountUpdated = Request.UpdateTotalCount;
    }

    public PaginationRequest Request { get; protected init; }

    /// <summary>
    /// Starts from 1.
    /// </summary>
    [JsonIgnore]
    public long PageNumber { get; protected init; }
    
    [JsonIgnore]
    public int PageSize { get; protected init; }

    public Guid FirstId { get; protected init; }
    public Guid LastId { get; protected init; }
    public int ItemCount { get; protected init; }

    public bool HasNext { get; protected init; }
    public bool HasPrevious { get; protected init; }

    public PaginationTotal Total { get; protected init; }
    public bool TotalCountUpdated { get; protected init; }

    public long GetTotalCountUpToCurrentPage(bool includeCurrentPage = true)
        => (PageNumber - 1) * PageSize + (includeCurrentPage ? ItemCount : 0);

    public PaginationRequest RequestNextPage(bool updateTotalCount = false, long totalCount = -1)
        => Request.GetNextPage(FirstId, LastId, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);

    public PaginationRequest RequestPreviousPage(bool updateTotalCount = false, long totalCount = -1)
        => Request.GetPreviousPage(FirstId, LastId, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);

    public PaginationRequest RequestRefresh(bool updateTotalCount = false, long totalCount = -1)
        => RequestPage(PageNumber, updateTotalCount, totalCount, HasNext);

    public PaginationRequest RequestPage(long page, bool updateTotalCount = false, long totalCount = -1, bool markAsHasNextOnRefresh = false)
        => Request.GetPage(FirstId, LastId, PageNumber, page, updateTotalCount, totalCount != -1 ? totalCount : Total.Count, markAsHasNextOnRefresh);

    public PaginationResultInfo ToResultInfo() => new(this);
}