namespace DRN.Framework.SharedKernel.Domain;

public enum PageSortDirection : byte
{
    AscendingByCreatedAt = 1,
    DescendingByCreatedAt
}

public readonly struct PageCursor(long pageNumber, Guid lastId, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt)
{
    public static PageCursor Initial => InitialWith(PageSortDirection.AscendingByCreatedAt);
    public static PageCursor InitialWith(PageSortDirection direction) => new(1, Guid.Empty, direction);

    /// <summary>
    /// Points previous page or first page if it is the first request
    /// </summary>
    public long PageNumber { get; } = pageNumber > 1 ? pageNumber : 1;

    /// <summary>
    /// Points previous page's last item
    /// </summary>
    public Guid LastId { get; } = lastId;

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

    public PaginationRequest GetNextPage(Guid lastId)
    {
        var nextPageNumber = PageNumber + 1;
        var cursor = new PageCursor(PageNumber, lastId, PageCursor.SortDirection);
        var nextRequest = new PaginationRequest(nextPageNumber, cursor, PageSize);

        return nextRequest;
    }

    public PaginationRequest GetPageJump(Guid lastId, long fromPage, long toPage)
    {
        var cursor = new PageCursor(fromPage, lastId, PageCursor.SortDirection);
        var jumpRequest = new PaginationRequest(toPage, cursor, PageSize);

        return jumpRequest;
    }
}

public readonly struct PaginationResult<TEntity> where TEntity : SourceKnownEntity
{
    public PaginationResult(IReadOnlyList<TEntity> items, PaginationRequest request, long totalCount = -1)
    {
        var excessCount = request.PageSize.Size + 1;
        var hasNext = items.Count == excessCount;

        Items = hasNext ? items.Take(request.PageSize.Size).ToArray() : items;
        PageNumber = request.PageNumber;
        PageSize = request.PageSize.Size;

        if (items.Count > 0)
        {
            LastId = request.PageCursor.SortDirection == PageSortDirection.AscendingByCreatedAt
                ? Items.Max()!.EntityId
                : Items.Min()!.EntityId;
        }
        else
            LastId = Guid.Empty;

        HasPrevious = PageNumber > 1;
        HasNext = hasNext;
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

    public Guid LastId { get; }
    public IReadOnlyList<TEntity> Items { get; }


    public long TotalCount { get; }
    public long TotalPages { get; }
    public bool TotalCountSpecified { get; }

    public bool HasNext { get; }
    public bool HasPrevious { get; }

    public long GetTotalCountUpToCurrentPage(bool includeCurrentPage = true) => (PageNumber - 1) * PageSize + (includeCurrentPage ? Items.Count : 0);
    public PaginationRequest GetNextPage(PaginationRequest previousRequest) => previousRequest.GetNextPage(LastId);
    public PaginationRequest GetPageJump(PaginationRequest previousRequest, long toPage) => previousRequest.GetPageJump(LastId, PageNumber, toPage);
}