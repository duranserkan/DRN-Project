namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Sorts by CreatedAt unless a different sort order is specified.
/// </summary>
public enum PageSortDirection : byte
{
    Ascending = 1,
    Descending = 2
}

public enum PageNavigationDirection : byte
{
    Next = 1,
    Previous = 2,
    Refresh = 3
}