using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResultInfo : PaginationResultBase
{
    public PaginationResultInfo(PaginationResultBase paginationResult) : base(paginationResult)
    {
    }

    [JsonConstructor]
    public PaginationResultInfo(PaginationRequest request, Guid firstId, Guid lastId, int itemCount, bool hasNext, bool hasPrevious, PaginationTotal total)
    {
        Request = request;
        PageNumber = request.PageNumber;
        PageSize = request.PageSize.Size;
        FirstId = firstId;
        LastId = lastId;
        ItemCount = itemCount;
        HasNext = hasNext;
        HasPrevious = hasPrevious;
        Total = total;
        TotalCountUpdated = request.UpdateTotalCount;
    }
}