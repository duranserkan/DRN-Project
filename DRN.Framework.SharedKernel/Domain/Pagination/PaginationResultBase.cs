using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public abstract class PaginationResultBase
{
    /// <summary>
    /// Required for ASP.NET Core model binding from query strings and form data.
    /// The framework needs a parameterless constructor to instantiate the object
    /// before setting properties during binding with application/x-www-form-urlencoded format.
    /// </summary>
    protected PaginationResultBase()
    {
    }

    [SetsRequiredMembers]
    protected PaginationResultBase(PaginationResultBase paginationResult)
    {
        Request = paginationResult.Request;
        FirstId = paginationResult.FirstId;
        LastId = paginationResult.LastId;
        ItemCount = paginationResult.ItemCount;
        HasNext = paginationResult.HasNext;
        HasPrevious = paginationResult.HasPrevious;
        Total = paginationResult.Total;
        TotalCountUpdated = paginationResult.TotalCountUpdated;
    }

    [Required]
    public required PaginationRequest Request { get; init; }

    public required Guid FirstId { get; init; }

    public required Guid LastId { get; init; }

    public required int ItemCount { get; init; }

    public required bool HasNext { get; init; }

    public required bool HasPrevious { get; init; }

    [Required]
    public required PaginationTotal Total { get; init; } = PaginationTotal.NotSpecified;

    public required bool TotalCountUpdated { get; init; }

    public long GetTotalCountUpToCurrentPage(bool includeCurrentPage = true)
        => (Request.PageNumber - 1) * Request.PageSize.Size + (includeCurrentPage ? ItemCount : 0);

    public PaginationRequest RequestNextPage(bool updateTotalCount = false, long totalCount = -1)
        => Request.GetNextPage(FirstId, LastId, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);

    public PaginationRequest RequestPreviousPage(bool updateTotalCount = false, long totalCount = -1)
        => Request.GetPreviousPage(FirstId, LastId, updateTotalCount, totalCount != -1 ? totalCount : Total.Count);

    public PaginationRequest RequestRefresh(bool updateTotalCount = false, long totalCount = -1)
        => RequestPage(Request.PageNumber, updateTotalCount, totalCount != -1 ? totalCount : Total.Count, HasNext);

    public PaginationRequest RequestPage(long page, bool updateTotalCount = false, long totalCount = -1, bool markAsHasNextOnRefresh = false)
        => Request.GetPage(FirstId, LastId, Request.PageNumber, page, updateTotalCount, totalCount != -1 ? totalCount : Total.Count, markAsHasNextOnRefresh);

    public PaginationResultInfo ToResultInfo() => new(this);
}