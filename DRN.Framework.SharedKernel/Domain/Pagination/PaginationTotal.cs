using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationTotal
{
    private readonly long _count = -1;
    private readonly long _pages = -1;
    public static PaginationTotal NotSpecified => new(-1, -1);
    
    [JsonConstructor]
    public PaginationTotal()
    {
    }

    [SetsRequiredMembers]
    public PaginationTotal(long count, int pageSize)
    {
        Count = count;
        Pages = count > -1 && pageSize > 0 ? (long)Math.Ceiling(count / (double)pageSize) : -1;
    }

    public required long Count
    {
        get => _count;
        init => _count = value <= -1 ? -1 : value;
    }

    public required long Pages
    {
        get => _pages;
        init => _pages = value <= -1 ? -1 : value;
    }

    public bool CountSpecified => Count > -1;
}