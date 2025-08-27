using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResultInfo : PaginationResultBase
{
    public PaginationResultInfo()
    {
    }

    [SetsRequiredMembers]
    public PaginationResultInfo(PaginationResultBase paginationResult) : base(paginationResult)
    {
    }

    [JsonConstructor]
    public PaginationResultInfo(PaginationRequest request, Guid firstId, Guid lastId, int itemCount, bool hasNext, bool hasPrevious, PaginationTotal total)
    {
        Request = request;
        FirstId = firstId;
        LastId = lastId;
        ItemCount = itemCount;
        HasNext = hasNext;
        HasPrevious = hasPrevious;
        Total = total;
        TotalCountUpdated = request.UpdateTotalCount;
    }
}