namespace DRN.Framework.SharedKernel.Domain;

public enum PageSortDirection
{
    AscendingByCreatedAt = 1,
    DescendingByCreatedAt
}

public readonly struct PageCursor(long pageNumber, Guid lastId, PageSortDirection direction = PageSortDirection.AscendingByCreatedAt)
{
    public static PageCursor Initial => new(0, Guid.Empty);
    public long PageNumber { get; } = pageNumber > 0 ? pageNumber : 0;
    public Guid LastId { get; } = lastId;
    public PageSortDirection SortDirection { get; } = direction;
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
    public long PageNumber { get; } = pageNumber < 1 ? 1 : pageNumber;
    public PageSize PageSize { get; } = pageSize;
    public PageCursor PageCursor { get; } = pageCursor;
    public bool UpdateTotalCount { get; } = updateTotalCount;
}

public readonly struct PaginationResult<TEntity> where TEntity : SourceKnownEntity
{
    public PaginationResult(IReadOnlyList<TEntity> items, PaginationRequest request, long totalCount = -1)
    {
        Items = items;
        PageNumber = request.PageNumber;
        PageSize = request.PageSize.Size;

        if (items.Count > 0)
        {
            LastId = request.PageCursor.SortDirection == PageSortDirection.AscendingByCreatedAt
                ? items.Max()!.EntityId
                : items.Min()!.EntityId;
        }
        else
            LastId = Guid.Empty;

        HasPrevious = PageNumber > 1;
        HasNext = items.Count == request.PageSize.Size;
        TotalCount = totalCount;

        if (totalCount < 0) return;

        TotalCountSpecified = true;
        TotalPages = (long)Math.Ceiling(TotalCount / (double)PageSize);
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