using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

//structs have implicit constructors
[method: JsonConstructor]
public readonly struct PageCursor(long pageNumber, Guid firstId, Guid lastId, PageSortDirection sortDirection = PageSortDirection.Ascending)
{
    public static PageCursor Initial => InitialWith(PageSortDirection.Ascending);
    public static PageCursor InitialWith(PageSortDirection direction) => new(1, Guid.Empty, Guid.Empty, direction);

    /// <summary>
    /// Points the previous page's last item or first page's first item if it is the first request
    /// Used for determining a fetch direction by comparing it with the request page number
    /// </summary>
    public long PageNumber { get; } = pageNumber > 1 ? pageNumber : 1;

    /// <summary>
    /// Points the previous page's last item or first page's first item if it is Guid.Empty
    /// Used for fetching next pages
    /// </summary>
    public Guid LastId { get; } = lastId;

    /// <summary>
    /// Points the previous page's first item or the first page's first item if it is Guid.Empty.
    /// Used for fetching previous pages
    /// </summary>
    public Guid FirstId { get; } = firstId;

    public PageSortDirection SortDirection { get; } = sortDirection;

    public bool IsFirstPage => PageNumber == 1;
    public bool IsFirstRequest => IsFirstPage && LastId == Guid.Empty;

    public bool Valid() => PageNumber >= 1 && (SortDirection == PageSortDirection.Ascending || SortDirection == PageSortDirection.Descending);
}