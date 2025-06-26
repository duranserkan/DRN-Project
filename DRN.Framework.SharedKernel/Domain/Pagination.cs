namespace DRN.Framework.SharedKernel.Domain;

public enum PageSortDirection : byte
{
    AscendingByCreatedAt = 1,
    DescendingByCreatedAt
}

public enum NavigationDirection : byte
{
    Next = 1,
    Previous,
    /// <summary>
    /// can be used to refresh the current page
    /// </summary>
    Same
}

public readonly struct PageCursor(long pageNumber, Guid firstId, Guid lastId, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt)
{
    public static PageCursor Initial => InitialWith(PageSortDirection.AscendingByCreatedAt);
    public static PageCursor InitialWith(PageSortDirection direction) => new(1, Guid.Empty, Guid.Empty, direction);

    /// <summary>
    /// Points previous page's last item or first page's first item if it is the first request
    /// Used for determining fetch direction by comparing it with request page number
    /// </summary>
    public long PageNumber { get; } = pageNumber > 1 ? pageNumber : 1;

    /// <summary>
    /// Points previous page's last item or first page's first item if it is Guid.Empty
    /// Used for fetching next pages
    /// </summary>
    public Guid LastId { get; } = lastId;

    /// <summary>
    /// Points previous page's first item or first page's first item if it is Guid.Empty.
    /// Used for fetching previous pages
    /// </summary>
    public Guid FirstId { get; } = firstId;

    public PageSortDirection SortDirection { get; } = direction;

    public bool IsFirstPage => PageNumber == 1;
    public bool IsFirstRequest => IsFirstPage && LastId == Guid.Empty;
}

public readonly struct PageSize
{
    public const int MaxSizeDefault = 100;
    public const int MaxSizeThreshold = 1000;

    public static PageSize Default => new(10);

    public PageSize(int size, int maxSize = MaxSizeDefault, bool overrideMaxsizeThreshold = false)
    {
        if (overrideMaxsizeThreshold)
            MaxSize = maxSize;
        else
            MaxSize = maxSize < MaxSizeThreshold ? maxSize : MaxSizeThreshold;

        if (MaxSize < 1)
            MaxSize = 1;

        Size = size > MaxSize ? MaxSize : size;
        if (Size < 1)
            Size = 1;
    }

    public int Size { get; }
    public int MaxSize { get; }
}

/// <summary>
/// Represents pagination parameters for fetching a page of data.
/// </summary>
public readonly struct PaginationRequest(long pageNumber, PageCursor pageCursor, PageSize pageSize, bool updateTotalCount = false)
{
    public static PaginationRequest Default => DefaultWith();

    public static PaginationRequest DefaultWith(int size = 10, bool updateTotalCount = false, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt) =>
        new(1, PageCursor.InitialWith(direction), new PageSize(size), updateTotalCount);

    public long PageNumber { get; } = pageNumber < 1 ? 1 : pageNumber;
    public PageSize PageSize { get; } = pageSize;
    public PageCursor PageCursor { get; } = pageCursor;
    public bool UpdateTotalCount { get; } = updateTotalCount;

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
            : NavigationDirection.Same;
    }

    public PaginationRequest GetNextPage(Guid firstId, Guid lastId)
    {
        var nextPageNumber = PageNumber + 1;
        var cursor = new PageCursor(PageNumber, firstId, lastId, PageCursor.SortDirection);
        var nextRequest = new PaginationRequest(nextPageNumber, cursor, PageSize);

        return nextRequest;
    }

    public PaginationRequest GetPreviousPage(Guid firstId, Guid lastId)
    {
        var previousPageNumber = PageNumber - 1;
        var cursor = new PageCursor(PageNumber, firstId, lastId, PageCursor.SortDirection);
        var nextRequest = new PaginationRequest(previousPageNumber, cursor, PageSize);

        return nextRequest;
    }

    public PaginationRequest GetPageJump(Guid firstId, Guid lastId, long fromPage, long toPage)
    {
        var cursor = new PageCursor(fromPage, firstId, lastId, PageCursor.SortDirection);
        var jumpRequest = new PaginationRequest(toPage, cursor, PageSize);

        return jumpRequest;
    }
}

//todo add external total count support, correct previous and next page flags when applicable
public readonly struct PaginationResult<TEntity> where TEntity : SourceKnownEntity
{
    public PaginationResult(IReadOnlyList<TEntity> items, PaginationRequest request, long totalCount = -1)
    {
        var excessCount = request.PageSize.Size + 1;
        var hasExcessCount = items.Count == excessCount;

        Items = items;
        if (hasExcessCount)
        {
            Items = request.NavigationDirection != NavigationDirection.Next
                ? items.Skip(1).Take(request.PageSize.Size).ToArray()
                : items.Take(request.PageSize.Size).ToArray();
        }

        PageNumber = request.PageNumber;
        PageSize = request.PageSize.Size;

        if (items.Count > 0)
        {
            var max = Items.Max()!;
            var min = Items.Min()!;

            if (request.PageCursor.SortDirection == PageSortDirection.AscendingByCreatedAt)
            {
                LastId = max.EntityId;
                FirstId = min.EntityId;
            }
            else
            {
                LastId = min.EntityId;
                FirstId = max.EntityId;
            }
        }
        else
        {
            LastId = Guid.Empty;
            FirstId = Guid.Empty;
        }

        HasPrevious = PageNumber > 1;
        HasNext = request.NavigationDirection == NavigationDirection.Previous ||
                  (request.NavigationDirection == NavigationDirection.Next && hasExcessCount);
        SamePageRequest = request.NavigationDirection == NavigationDirection.Same;

        TotalCount = totalCount;
        TotalCountSpecified = totalCount > -1;
        TotalPages = TotalCountSpecified ? (long)Math.Ceiling(TotalCount / (double)PageSize) : -1;

        if (!TotalCountSpecified) return;
        HasNext = PageNumber < TotalPages;
    }

    /// <summary>
    /// Starts from 1.
    /// </summary>
    public long PageNumber { get; }

    public int PageSize { get; }

    public Guid FirstId { get; }
    public Guid LastId { get; }
    public IReadOnlyList<TEntity> Items { get; }

    public long TotalCount { get; }
    public long TotalPages { get; }
    public bool TotalCountSpecified { get; }

    public bool HasNext { get; }
    public bool HasPrevious { get; }

    /// <summary>
    /// Page refreshed
    /// </summary>
    public bool SamePageRequest { get; }

    public long GetTotalCountUpToCurrentPage(bool includeCurrentPage = true) => (PageNumber - 1) * PageSize + (includeCurrentPage ? Items.Count : 0);
    public PaginationRequest GetNextPage(PaginationRequest previousPageRequest) => previousPageRequest.GetNextPage(FirstId, LastId);
    public PaginationRequest GetPreviousPage(PaginationRequest nextPageRequest) => nextPageRequest.GetPreviousPage(FirstId, LastId);
    public PaginationRequest GetPageJump(PaginationRequest previousRequest, long toPage) => previousRequest.GetPageJump(FirstId, LastId, PageNumber, toPage);
}