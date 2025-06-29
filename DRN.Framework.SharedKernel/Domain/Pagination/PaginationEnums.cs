namespace DRN.Framework.SharedKernel.Domain.Pagination;

public enum PageSortDirection : byte
{
    AscendingByCreatedAt = 1,
    DescendingByCreatedAt
}

public enum NavigationDirection : byte
{
    Next = 1,
    Previous,
    Refresh
}