using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PageCursor
{
    private readonly long _pageNumber = 1;
    private readonly Guid _lastId = Guid.Empty;
    private readonly Guid _firstId = Guid.Empty;
    private readonly PageSortDirection _sortDirection = PageSortDirection.Ascending;
    
    /// <summary>
    /// Required for ASP.NET Core model binding from query strings and form data.
    /// The framework needs a parameterless constructor to instantiate the object
    /// before setting properties during binding with application/x-www-form-urlencoded format.
    /// </summary>
    public PageCursor()
    {
    }

    [JsonConstructor]
    [SetsRequiredMembers]
    public PageCursor(long pageNumber, Guid firstId, Guid lastId, PageSortDirection sortDirection = PageSortDirection.Ascending)
    {
        PageNumber = pageNumber;
        LastId = lastId;
        FirstId = firstId;
        SortDirection = sortDirection;
    }

    public static PageCursor Initial => InitialWith(PageSortDirection.Ascending);
    public static PageCursor InitialWith(PageSortDirection direction) => new(1, Guid.Empty, Guid.Empty, direction);

    /// <summary>
    /// Points the previous page's last item or first page's first item if it is the first request
    /// Used for determining a fetch direction by comparing it with the request page number
    /// </summary>
    public required long PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value > 1 ? value : 1;
    }

    /// <summary>
    /// Points the previous page's last item or first page's first item if it is Guid.Empty
    /// Used for fetching next pages
    /// </summary>
    public required Guid LastId
    {
        get => _lastId;
        init => _lastId = value;
    }

    /// <summary>
    /// Points the previous page's first item or the first page's first item if it is Guid.Empty.
    /// Used for fetching previous pages
    /// </summary>
    public required Guid FirstId
    {
        get => _firstId;
        init => _firstId = value;
    }

    public required PageSortDirection SortDirection
    {
        get => _sortDirection;
        init => _sortDirection = value;
    }

    [JsonIgnore]
    public bool IsFirstPage => PageNumber == 1;

    [JsonIgnore]
    public bool IsFirstRequest => IsFirstPage && LastId == Guid.Empty;

    public bool Valid() => PageNumber >= 1 && (SortDirection == PageSortDirection.Ascending || SortDirection == PageSortDirection.Descending);
}