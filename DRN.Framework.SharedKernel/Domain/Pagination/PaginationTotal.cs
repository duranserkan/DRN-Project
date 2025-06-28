namespace DRN.Framework.SharedKernel.Domain.Pagination;

public readonly record struct PaginationTotal(long Count, int PageSize)
{
    public static PaginationTotal NotSpecified() => new(-1, -1);

    public long Pages { get; } = Count > -1
        ? (long)Math.Ceiling(Count / (double)PageSize)
        : -1;

    public bool CountSpecified { get; } = Count > -1;
}