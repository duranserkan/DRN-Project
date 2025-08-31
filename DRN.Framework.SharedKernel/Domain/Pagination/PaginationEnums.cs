namespace DRN.Framework.SharedKernel.Domain.Pagination;

/// <summary>
/// Sorts by CreatedAt unless a different sort order is specified.
/// </summary>
public enum PageSortDirection : byte
{
    Ascending = 1,
    Descending = 2,
    /// <summary>
    /// Indicates that no specific sort direction is set. A default direction, typically Ascending,
    /// will be applied based on the underlying field name (e.g., 'Id', 'CreatedAt').
    /// </summary>
    None=3
}

public enum PageNavigationDirection : byte
{
    Next = 1,
    Previous = 2,
    Refresh = 3
}