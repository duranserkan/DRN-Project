using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Represents pagination parameters for fetching a page of data.
/// </summary>
public class PaginationRequest
{
    private readonly long _pageNumber = 1;
    private readonly PageSize _pageSize = PageSize.Default;
    private readonly PageCursor _pageCursor = PageCursor.Initial;
    private readonly long _totalCount = -1;

    /// <summary>
    /// Required for ASP.NET Core model binding from query strings and form data.
    /// The framework needs a parameterless constructor to instantiate the object
    /// before setting properties during binding with application/x-www-form-urlencoded format.
    /// </summary>
    public PaginationRequest()
    {
    }

    //todo test default values
    /// <summary>
    /// Represents pagination parameters for fetching a page of data.
    /// </summary>
    public PaginationRequest(
        long pageNumber,
        PageSize? pageSize = null,
        PageCursor? pageCursor = null,
        long totalCount = -1,
        bool updateTotalCount = false,
        bool markAsHasNextOnRefresh = false)
    {
        PageNumber = pageNumber;
        PageSize = pageSize ?? PageSize.Default;
        PageCursor = pageCursor ?? PageCursor.Initial;
        TotalCount = totalCount;
        UpdateTotalCount = updateTotalCount;
        MarkAsHasNextOnRefresh = markAsHasNextOnRefresh;
    }

    public static PaginationRequest Default => DefaultWith();

    public static PaginationRequest DefaultWith(int size = PageSize.SizeDefault, int maxSize = PageSize.MaxSizeDefault, PageSortDirection direction = PageSortDirection.Ascending,
        long totalCount = -1, bool updateTotalCount = false) =>
        new(1, new PageSize(size, maxSize), PageCursor.InitialWith(direction), totalCount, updateTotalCount: updateTotalCount);

    public long PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public PageSize PageSize
    {
        get => _pageSize;
        init => _pageSize = value.Valid() ? value : PageSize.Default;
    }

    public PageCursor PageCursor
    {
        get => _pageCursor;
        init => _pageCursor = value.Valid() ? value : PageCursor.Initial;
    }

    public long TotalCount
    {
        get => _totalCount;
        init => _totalCount = value < -1 ? -1 : value;
    }

    public bool UpdateTotalCount { get; init; }
    public bool MarkAsHasNextOnRefresh { get; init; }


    [JsonIgnore]
    public PaginationTotal Total => new(TotalCount, PageSize.Size);

    [JsonIgnore]
    public long PageDifference => PageNumber > PageCursor.PageNumber
        ? PageNumber - PageCursor.PageNumber
        : PageCursor.PageNumber - PageNumber;

    [JsonIgnore]
    public bool IsPageRefresh => NavigationDirection == PageNavigationDirection.Refresh;

    [JsonIgnore]
    public PageNavigationDirection NavigationDirection => CalculateDirection(PageNumber, PageCursor.PageNumber, PageCursor.IsFirstRequest);

    public Guid GetCursorId() => NavigationDirection == PageNavigationDirection.Next
        ? PageCursor.LastId
        : PageCursor.FirstId;

    public bool IsPageJump() => PageDifference > 1;
    public int GetSkipSize() => IsPageJump() ? (int)((PageDifference - 1) * PageSize.Size) : 0;

    private static PageNavigationDirection CalculateDirection(long pageNumber, long cursorPageNumber, bool firstRequest)
    {
        if (pageNumber > cursorPageNumber || firstRequest)
            return PageNavigationDirection.Next;

        return pageNumber < cursorPageNumber
            ? PageNavigationDirection.Previous
            : PageNavigationDirection.Refresh;
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