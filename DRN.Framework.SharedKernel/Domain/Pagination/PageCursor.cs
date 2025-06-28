namespace DRN.Framework.SharedKernel.Domain.Pagination;

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