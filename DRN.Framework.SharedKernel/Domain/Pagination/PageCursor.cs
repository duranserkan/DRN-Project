using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public readonly struct PageCursor
{
    [JsonConstructor] //structs have implicit constructors
    public PageCursor(long pageNumber, Guid firstId, Guid lastId, PageSortDirection sortDirection = PageSortDirection.AscendingByCreatedAt)
    {
        PageNumber = pageNumber > 1 ? pageNumber : 1;
        LastId = lastId;
        FirstId = firstId;
        SortDirection = sortDirection;
    }

    public static PageCursor Initial => InitialWith(PageSortDirection.AscendingByCreatedAt);
    public static PageCursor InitialWith(PageSortDirection direction) => new(1, Guid.Empty, Guid.Empty, direction);

    /// <summary>
    /// Points previous page's last item or first page's first item if it is the first request
    /// Used for determining fetch direction by comparing it with request page number
    /// </summary>
    public long PageNumber { get; }

    /// <summary>
    /// Points previous page's last item or first page's first item if it is Guid.Empty
    /// Used for fetching next pages
    /// </summary>
    public Guid LastId { get; }

    /// <summary>
    /// Points previous page's first item or first page's first item if it is Guid.Empty.
    /// Used for fetching previous pages
    /// </summary>
    public Guid FirstId { get; }

    public PageSortDirection SortDirection { get; }

    public bool IsFirstPage => PageNumber == 1;
    public bool IsFirstRequest => IsFirstPage && LastId == Guid.Empty;
}