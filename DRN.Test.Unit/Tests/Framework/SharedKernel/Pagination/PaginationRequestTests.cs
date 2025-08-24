using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationRequestTests
{
    [Fact]
    public void PaginationRequest_Should_Be_Deserialized()
    {
        var request = PaginationRequest.Default;
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        request.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        request.PageCursor.Should().Be(PageCursor.Initial);
        request.PageSize.Should().Be(PageSize.Default);
        request.PageNumber.Should().Be(1);
        request.PageDifference.Should().Be(0);
        request.Total.Should().Be(PaginationTotal.NotSpecified);
        request.UpdateTotalCount.Should().BeFalse();
        request.GetCursorId().Should().BeEmpty();
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();

        request = PaginationRequest.DefaultWith(50, 150, true, PageSortDirection.Descending);
        request.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        request.PageCursor.Should().Be(new PageCursor(1, Guid.Empty, Guid.Empty, PageSortDirection.Descending));
        request.PageSize.Should().Be(new PageSize(50, 150));
        request.PageNumber.Should().Be(1);
        request.PageDifference.Should().Be(0);
        request.Total.Should().Be(PaginationTotal.NotSpecified);
        request.UpdateTotalCount.Should().BeTrue();
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        request.GetCursorId().Should().BeEmpty();
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();

        var firstId = Guid.NewGuid();
        var lastId = Guid.NewGuid();
        var pageSize = new PageSize(13, 28);
        var pageCursor = new PageCursor(5, firstId, lastId, PageSortDirection.Descending);
        var pageNumber = 3;
        var totalCount = 73;

        request = new PaginationRequest(pageNumber, pageSize, pageCursor, totalCount, true, true);
        request.NavigationDirection.Should().Be(PageNavigationDirection.Previous);
        request.PageSize.Should().Be(pageSize);
        request.PageCursor.Should().Be(pageCursor);
        request.PageNumber.Should().Be(pageNumber);
        request.PageDifference.Should().Be(2);
        request.Total.Should().Be(new PaginationTotal(totalCount, pageSize.Size));
        request.UpdateTotalCount.Should().BeTrue();
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeTrue();
        request.GetCursorId().Should().Be(firstId);
        request.IsPageJump().Should().BeTrue();
        request.GetSkipSize().Should().Be(pageSize.Size);
        request.ValidateObjectSerialization();


        var firstId2 = Guid.NewGuid();
        var lastId2 = Guid.NewGuid();
        var pageCursor2 = new PageCursor(3, firstId2, lastId2, PageSortDirection.Descending);
        var totalCount2 = 90;
        request = request.GetPage(firstId2, lastId2, pageNumber, pageNumber, true, totalCount2, true);
        request.NavigationDirection.Should().Be(PageNavigationDirection.Refresh);
        request.PageSize.Should().Be(pageSize);
        request.PageCursor.Should().Be(pageCursor2);
        request.PageNumber.Should().Be(pageNumber);
        request.PageDifference.Should().Be(0);
        request.Total.Should().Be(new PaginationTotal(totalCount2, pageSize.Size));
        request.UpdateTotalCount.Should().BeTrue();
        request.IsPageRefresh.Should().BeTrue();
        request.MarkAsHasNextOnRefresh.Should().BeTrue();
        request.GetCursorId().Should().Be(firstId2);
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();

        var pageCursor3 = new PageCursor(pageNumber, firstId2, lastId2, PageSortDirection.Descending);
        var totalCount3 = 95;
        request = request.GetNextPage(firstId2, lastId2, true, totalCount3);
        request.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        request.PageSize.Should().Be(pageSize);
        request.PageCursor.Should().Be(pageCursor3);
        request.PageNumber.Should().Be(pageNumber + 1);
        request.PageDifference.Should().Be(1);
        request.Total.Should().Be(new PaginationTotal(totalCount3, pageSize.Size));
        request.UpdateTotalCount.Should().BeTrue();
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        request.GetCursorId().Should().Be(lastId2);
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();

        var pageCursor4 = new PageCursor(pageNumber + 1, firstId2, lastId2, PageSortDirection.Descending);
        request = request.GetPreviousPage(firstId2, lastId2, true, totalCount3 + 1);
        request.NavigationDirection.Should().Be(PageNavigationDirection.Previous);
        request.PageSize.Should().Be(pageSize);
        request.PageCursor.Should().Be(pageCursor4);
        request.PageNumber.Should().Be(pageNumber);
        request.PageDifference.Should().Be(1);
        request.Total.Should().Be(new PaginationTotal(totalCount3 + 1, pageSize.Size));
        request.UpdateTotalCount.Should().BeTrue();
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        request.GetCursorId().Should().Be(firstId2);
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();
    }
}