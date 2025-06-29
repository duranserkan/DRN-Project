namespace DRN.Framework.SharedKernel.Domain.Pagination;

public readonly struct PaginationTotal(long count, int pageSize)
{
    public static PaginationTotal NotSpecified() => new(-1, -1);

    public long Count { get; init; } = count;

    public long Pages { get; init; } = count > -1
        ? (long)Math.Ceiling(count / (double)pageSize)
        : -1;

    public bool CountSpecified { get; init; } = count > -1;
}