using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Represents pagination parameters for fetching a page of data.
/// </summary>
public class PaginationRequest
{
    //todo test default values
    /// <summary>
    /// Represents pagination parameters for fetching a page of data.
    /// </summary>
    public PaginationRequest(
        long pageNumber = 1,
        PageSize pageSize = default,
        PageCursor pageCursor = default,
        long totalCount = -1,
        bool updateTotalCount = false,
        bool markAsHasNextOnRefresh = false)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = pageSize.Valid() ? pageSize : PageSize.Default;
        PageCursor = pageCursor.Valid() ? pageCursor : PageCursor.Initial;
        Total = new PaginationTotal(totalCount, PageSize.Size);
        UpdateTotalCount = updateTotalCount;
        MarkAsHasNextOnRefresh = markAsHasNextOnRefresh;
    }

    public static PaginationRequest Default => DefaultWith();

    public static PaginationRequest DefaultWith(int size = PageSize.SizeDefault, bool updateTotalCount = false,
        PageSortDirection direction = PageSortDirection.AscendingByCreatedAt) =>
        new(1, new PageSize(size), PageCursor.InitialWith(direction), updateTotalCount: updateTotalCount);

    public long PageNumber { get; init; }
    public PageSize PageSize { get; init; }
    public PageCursor PageCursor { get; init; }

    public bool UpdateTotalCount { get; init; }
    public bool MarkAsHasNextOnRefresh { get; init; }

    public long TotalCount => Total.Count;

    [JsonIgnore]
    public PaginationTotal Total { get; }

    [JsonIgnore]
    public long PageDifference => PageNumber > PageCursor.PageNumber
        ? PageNumber - PageCursor.PageNumber
        : PageCursor.PageNumber - PageNumber;

    [JsonIgnore]
    public bool IsPageRefresh => NavigationDirection == NavigationDirection.Refresh;

    [JsonIgnore]
    public NavigationDirection NavigationDirection => CalculateDirection(PageNumber, PageCursor.PageNumber, PageCursor.IsFirstRequest);

    public Guid GetCursorId() => NavigationDirection == NavigationDirection.Next
        ? PageCursor.LastId
        : PageCursor.FirstId;

    public bool IsPageJump() => PageDifference > 1;
    public int GetSkipSize() => IsPageJump() ? (int)((PageDifference - 1) * PageSize.Size) : 0;

    private static NavigationDirection CalculateDirection(long pageNumber, long cursorPageNumber, bool firstRequest)
    {
        if (pageNumber > cursorPageNumber || firstRequest)
            return NavigationDirection.Next;

        return pageNumber < cursorPageNumber
            ? NavigationDirection.Previous
            : NavigationDirection.Refresh;
    }

    public PaginationRequest GetNextPage(Guid firstId, Guid lastId, bool updateTotalCount = false, long totalCount = -1)
    {
        var nextPageNumber = PageNumber + 1;
        var nextRequest = GetPage(firstId, lastId, PageNumber, nextPageNumber, updateTotalCount, totalCount);

        return nextRequest;
    }

    public PaginationRequest GetPreviousPage(Guid firstId, Guid lastId, bool updateTotalCount = false, long totalCount = -1)
    {
        var previousPageNumber = PageNumber - 1;
        var nextRequest = GetPage(firstId, lastId, PageNumber, previousPageNumber, updateTotalCount, totalCount);

        return nextRequest;
    }

    public PaginationRequest GetPage(Guid firstId, Guid lastId, long fromPage, long toPage, bool updateTotalCount = false, long totalCount = -1,
        bool markAsHasNextOnRefresh = false)
    {
        var cursor = new PageCursor(fromPage, firstId, lastId, PageCursor.SortDirection);
        var pageRequest = new PaginationRequest(toPage, PageSize, cursor, totalCount != -1 ? totalCount : Total.Count, updateTotalCount, markAsHasNextOnRefresh);

        return pageRequest;
    }
}