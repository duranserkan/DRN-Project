namespace DRN.Framework.SharedKernel.Domain;

public enum PageSortDirection
{
    AscendingByCreatedAt = 1,
    DescendingByCreatedAt
}

public readonly struct PageCursor(long pageNumber, Guid lastId, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt)
{
    public static PageCursor Initial => new(1, Guid.Empty);
    public long PageNumber { get; } = pageNumber > 1 ? pageNumber : 1;
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
        MaxSize = maxSize < MaxSizeThreshold || overrideMaxsizeThreshold ? maxSize : MaxSizeDefault;
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
    public static PaginationRequest Default => new(1, PageCursor.Initial, PageSize.Default, false);
    public static PaginationRequest DefaultWith(int size = 10, bool updateTotalCount = false) => new(1, PageCursor.Initial, new PageSize(size), updateTotalCount);

    public long PageNumber { get; } = pageNumber < 1 ? 1 : pageNumber;
    public PageSize PageSize { get; } = pageSize;
    public PageCursor PageCursor { get; } = pageCursor;
    public bool UpdateTotalCount { get; } = updateTotalCount;


    public PaginationRequest GetNextPage(Guid lastId)
    {
        var nextPageNumber = PageNumber + 1;
        var nextCursor = new PageCursor(nextPageNumber, lastId, PageCursor.SortDirection);
        var nextRequest = new PaginationRequest(nextPageNumber, nextCursor, PageSize);

        return nextRequest;
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
}