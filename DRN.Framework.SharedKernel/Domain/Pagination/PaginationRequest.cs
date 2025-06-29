namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Represents pagination parameters for fetching a page of data.
/// </summary>
public readonly struct PaginationRequest
{
    /// <summary>
    /// Represents pagination parameters for fetching a page of data.
    /// </summary>
    public PaginationRequest(long pageNumber,
        PageCursor pageCursor,
        PageSize pageSize,
        bool updateTotalCount = false,
        long totalCount = -1,
        bool markAsHasNextOnRefresh = false)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = pageSize;
        PageCursor = pageCursor;
        Total = new PaginationTotal(totalCount, pageSize.Size);
        UpdateTotalCount = updateTotalCount;
        MarkAsHasNextOnRefresh = markAsHasNextOnRefresh;
        PageDifference = pageNumber > pageCursor.PageNumber
            ? pageNumber - pageCursor.PageNumber
            : pageCursor.PageNumber - pageNumber;
        NavigationDirection = CalculateDirection(pageNumber, pageCursor.PageNumber, pageCursor.IsFirstRequest);
    }

    public static PaginationRequest Default => DefaultWith();

    public static PaginationRequest DefaultWith(int size = 10, bool updateTotalCount = false, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt) =>
        new(1, PageCursor.InitialWith(direction), new PageSize(size), updateTotalCount);

    public long PageNumber { get; init; }
    public PageSize PageSize { get; init; }
    public PageCursor PageCursor { get; init; }
    public PaginationTotal Total { get; init; }
    public bool UpdateTotalCount { get; init; }
    public bool MarkAsHasNextOnRefresh { get; init; }

    public long PageDifference { get; init; }

    public bool IsPageJump() => PageDifference > 1;
    public int GetSkipSize() => IsPageJump() ? (int)((PageDifference - 1) * PageSize.Size) : 0;
    public bool IsPageRefresh => NavigationDirection == NavigationDirection.Refresh;
    public NavigationDirection NavigationDirection { get; init; }

    public Guid GetCursorId() => NavigationDirection == NavigationDirection.Next
        ? PageCursor.LastId
        : PageCursor.FirstId;

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
        var pageRequest = new PaginationRequest(toPage, cursor, PageSize, updateTotalCount, totalCount != -1 ? totalCount : Total.Count, markAsHasNextOnRefresh);

        return pageRequest;
    }
}