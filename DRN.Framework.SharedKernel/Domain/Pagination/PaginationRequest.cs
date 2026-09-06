using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Represents pagination parameters for fetching a page of data.
/// </summary>
public class PaginationRequest
{
    /// <summary>
    /// Required for ASP.NET Core model binding from query strings and form data.
    /// The framework needs a parameterless constructor to instantiate the object
    /// before setting properties during binding with application/x-www-form-urlencoded format.
    /// </summary>
    public PaginationRequest()
    {
    }

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

    public static PaginationRequest FromOffset(PaginationResultInfo? resultInfo = null, int skip = -1, int take = -1, int maxSize = -1,
        PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false)
    {
        var pageSize = take < 1 ? PageSize.SizeDefault : take;
        var page = skip < 1 || take < 1 ? 1 : skip / take + 1;

        return From(resultInfo, page, pageSize, maxSize, direction, totalCount, updateTotalCount);
    }

    public static PaginationRequest From(PaginationResultInfo? resultInfo = null, long jumpTo = 1, int pageSize = -1, int maxSize = -1,
        PageSortDirection direction = PageSortDirection.None, long totalCount = -1, bool updateTotalCount = false)
    {
        totalCount = ResolveTotalCount(resultInfo, totalCount);
        maxSize = Math.Min(maxSize, PageSize.MaxSizeThreshold);

        if (resultInfo is null || HasSettingsChanged(resultInfo, pageSize, maxSize, direction))
            return CreateResetRequest(resultInfo, pageSize, maxSize, direction, totalCount, updateTotalCount);

        var targetPage = ClampJumpPage(resultInfo.Request.PageNumber, jumpTo);
        return targetPage == resultInfo.Request.PageNumber
            ? resultInfo.RequestRefresh(updateTotalCount)
            : resultInfo.RequestPage(targetPage, updateTotalCount);
    }

    public long PageNumber
    {
        get;
        init => field = value < 1 ? 1 : value;
    } = 1;

    public PageSize PageSize
    {
        get;
        init => field = value.Valid() ? value : PageSize.Default;
    } = PageSize.Default;

    public PageCursor PageCursor
    {
        get;
        init => field = value.Valid() ? value : PageCursor.Initial;
    } = PageCursor.Initial;

    public long TotalCount
    {
        get;
        init => field = value < -1 ? -1 : value;
    } = -1;

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
    public int GetSkipSize()
    {
        var pageDifference = PageDifference;
        if (pageDifference <= 1)
            return 0;

        var pagesToSkip = pageDifference - 1;
        var pageSize = PageSize.Size;
        if (pagesToSkip > int.MaxValue / pageSize)
            throw ExceptionFor.Validation("Pagination offset exceeds the maximum supported skip size.");

        return (int)(pagesToSkip * pageSize);
    }

    private static PageNavigationDirection CalculateDirection(long pageNumber, long cursorPageNumber, bool firstRequest)
    {
        if (pageNumber > cursorPageNumber || firstRequest)
            return PageNavigationDirection.Next;

        return pageNumber < cursorPageNumber
            ? PageNavigationDirection.Previous
            : PageNavigationDirection.Refresh;
    }

    private static long ResolveTotalCount(PaginationResultInfo? resultInfo, long totalCount) =>
        resultInfo is not null && totalCount < 1 ? resultInfo.Total.Count : totalCount;

    private static bool HasSettingsChanged(PaginationResultInfo resultInfo, int pageSize, int maxSize, PageSortDirection direction)
    {
        var directionChanged = direction != PageSortDirection.None && direction != resultInfo.Request.PageCursor.SortDirection;
        var sizeChanged = pageSize > 0 && pageSize != resultInfo.Request.PageSize.Size;
        var maxSizeChanged = maxSize > 0 && maxSize != resultInfo.Request.PageSize.MaxSize;

        return directionChanged || sizeChanged || maxSizeChanged;
    }

    private static PaginationRequest CreateResetRequest(
        PaginationResultInfo? resultInfo,
        int pageSize,
        int maxSize,
        PageSortDirection direction,
        long totalCount,
        bool updateTotalCount)
    {
        var resolvedSize = pageSize > 0 ? pageSize : resultInfo?.Request.PageSize.Size ?? PageSize.SizeDefault;
        var resolvedMaxSize = maxSize > 0 ? maxSize : resultInfo?.Request.PageSize.MaxSize ?? PageSize.MaxSizeDefault;
        var resolvedDirection = direction != PageSortDirection.None ? direction : resultInfo?.Request.PageCursor.SortDirection ?? PageSortDirection.Ascending;

        return DefaultWith(resolvedSize, resolvedMaxSize, resolvedDirection, totalCount: totalCount, updateTotalCount: updateTotalCount);
    }

    private static long ClampJumpPage(long currentPage, long jumpTo)
    {
        var min = Math.Max(1, currentPage - 10);
        var max = currentPage > long.MaxValue - 10 ? long.MaxValue : currentPage + 10;
        return Math.Clamp(jumpTo, min, max);
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
