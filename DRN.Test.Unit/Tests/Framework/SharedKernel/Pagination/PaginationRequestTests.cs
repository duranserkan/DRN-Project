using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationRequestTests
{
    [Fact]
    public void PaginationRequest_Should_Be_Deserialized()
    {
        var request = PaginationRequest.Default;
        request.ValidateObjectSerialization();

        request = PaginationRequest.DefaultWith(50, true, PageSortDirection.DescendingByCreatedAt);
        request.ValidateObjectSerialization();
        
        var pageSize = new PageSize(13, 28);
        var pageCursor = new PageCursor(2, Guid.NewGuid(), Guid.NewGuid(), PageSortDirection.DescendingByCreatedAt);
        
        request = new PaginationRequest(3, pageCursor, pageSize, true, 73, true);
        request.ValidateObjectSerialization();
        
        request = new PaginationRequest(1, pageCursor, pageSize, true, 73, true);
        request.ValidateObjectSerialization();
    }
}