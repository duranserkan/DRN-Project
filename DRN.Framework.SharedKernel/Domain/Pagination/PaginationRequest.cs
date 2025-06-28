namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Represents pagination parameters for fetching a page of data.
/// </summary>
public readonly struct PaginationRequest(
    long pageNumber,
    PageCursor pageCursor,
    PageSize pageSize,
    bool updateTotalCount = false,
    long totalCount = -1,
    bool markAsHasNextOnRefresh = false)
{
    public static PaginationRequest Default => DefaultWith();

    public static PaginationRequest DefaultWith(int size = 10, bool updateTotalCount = false, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt) =>
        new(1, PageCursor.InitialWith(direction), new PageSize(size), updateTotalCount);

    public long PageNumber { get; } = pageNumber < 1 ? 1 : pageNumber;
    public PageSize PageSize { get; } = pageSize;
    public PageCursor PageCursor { get; } = pageCursor;
    public PaginationTotal Total { get; } = new(totalCount, pageSize.Size);
    public bool UpdateTotalCount { get; } = updateTotalCount;
    public bool MarkAsHasNextOnRefresh { get; } = markAsHasNextOnRefresh;

    public long PageDifference { get; } = pageNumber > pageCursor.PageNumber
        ? pageNumber - pageCursor.PageNumber
        : pageCursor.PageNumber - pageNumber;

    public bool IsPageJump() => PageDifference > 1;
    public int GetSkipSize() => (int)((PageDifference - 1) * PageSize.Size);
    public bool IsPageRefresh => NavigationDirection == NavigationDirection.Refresh;
    public NavigationDirection NavigationDirection { get; } = CalculateDirection(pageNumber, pageCursor.PageNumber, pageCursor.IsFirstRequest);

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