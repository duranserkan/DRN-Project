using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationRequestTests
{
    [Theory]
    [DataInlineUnit(1L, 1L, 10, 0)]
    [DataInlineUnit(1L, 2L, 10, 0)]
    [DataInlineUnit(2L, 1L, 10, 0)]
    [DataInlineUnit(2L, 100L, 10, 970)]
    [DataInlineUnit(100L, 2L, 10, 970)]
    [DataInlineUnit(1L, 2147483649L, 1, int.MaxValue)]
    [DataInlineUnit(2147483649L, 1L, 1, int.MaxValue)]
    [DataInlineUnit(1L, 214748366L, 10, 2147483640)]
    [DataInlineUnit(long.MaxValue - 2, long.MaxValue, 10, 10)]
    public void GetSkipSize_Should_Preserve_Representable_Offsets(long cursorPage, long targetPage, int size, int expectedSkip)
    {
        var request = new PaginationRequest(targetPage, new PageSize(size), new PageCursor(cursorPage, Guid.Empty, Guid.Empty));

        request.GetSkipSize().Should().Be(expectedSkip);
    }

    [Theory]
    [DataInlineUnit(1L, 2147483650L, 1)]
    [DataInlineUnit(2147483650L, 1L, 1)]
    [DataInlineUnit(1L, 214748367L, 10)]
    [DataInlineUnit(214748367L, 1L, 10)]
    [DataInlineUnit(1L, long.MaxValue, 10)]
    [DataInlineUnit(long.MaxValue, 1L, 10)]
    public void GetSkipSize_Should_Reject_Unrepresentable_Offsets(long cursorPage, long targetPage, int size)
    {
        var request = new PaginationRequest
        {
            PageNumber = targetPage,
            PageSize = new PageSize(size),
            PageCursor = new PageCursor(cursorPage, Guid.Empty, Guid.Empty)
        };

        var act = () => request.GetSkipSize();

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void RequestPage_Should_Allow_Large_Jumps_But_Reject_Unrepresentable_Skip_Size()
    {
        var resultInfo = new PaginationResultInfo(new PaginationRequest(2), Guid.NewGuid(), Guid.NewGuid(), 10, true, true, PaginationTotal.NotSpecified);

        var request = resultInfo.RequestPage(100);
        request.PageNumber.Should().Be(100);
        request.GetSkipSize().Should().Be(970);

        var excessiveRequest = resultInfo.RequestPage(long.MaxValue);
        var act = () => excessiveRequest.GetSkipSize();
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void From_Should_Use_Default_Settings_For_Initial_Request()
    {
        var request = PaginationRequest.From();

        request.Should().BeEquivalentTo(PaginationRequest.Default);

        var descendingRequest = PaginationRequest.From(direction: PageSortDirection.Descending);
        descendingRequest.PageSize.Should().BeEquivalentTo(PageSize.Default);
        descendingRequest.PageCursor.Should().BeEquivalentTo(PageCursor.InitialWith(PageSortDirection.Descending));
    }

    [Theory]
    [DataInlineUnit(-1, -1, PageSortDirection.Ascending, 50, 150, PageSortDirection.Ascending)]
    [DataInlineUnit(60, -1, PageSortDirection.None, 60, 150, PageSortDirection.Descending)]
    [DataInlineUnit(60, 200, PageSortDirection.Ascending, 60, 200, PageSortDirection.Ascending)]
    public void From_Should_Preserve_Omitted_Settings_When_Resetting(
        int pageSize, int maxSize, PageSortDirection direction, int expectedSize, int expectedMaxSize, PageSortDirection expectedDirection)
    {
        var currentRequest = new PaginationRequest(5, new PageSize(50, 150), PageCursor.InitialWith(PageSortDirection.Descending));
        var resultInfo = new PaginationResultInfo(currentRequest, Guid.NewGuid(), Guid.NewGuid(), 50, true, true, new PaginationTotal(500, 50));

        var request = PaginationRequest.From(resultInfo, jumpTo: 6, pageSize: pageSize, maxSize: maxSize, direction: direction, updateTotalCount: true);

        request.PageNumber.Should().Be(1);
        request.PageSize.Size.Should().Be(expectedSize);
        request.PageSize.MaxSize.Should().Be(expectedMaxSize);
        request.PageCursor.Should().BeEquivalentTo(PageCursor.InitialWith(expectedDirection));
        request.TotalCount.Should().Be(500);
        request.UpdateTotalCount.Should().BeTrue();
        currentRequest.PageSize.Should().BeEquivalentTo(new PageSize(50, 150));
        currentRequest.PageCursor.SortDirection.Should().Be(PageSortDirection.Descending);
    }

    [Fact]
    public void From_Should_Preserve_Settings_And_Cursor_When_Navigating()
    {
        var currentRequest = new PaginationRequest(5, new PageSize(50, 150), PageCursor.InitialWith(PageSortDirection.Descending));
        var resultInfo = new PaginationResultInfo(currentRequest, Guid.NewGuid(), Guid.NewGuid(), 50, true, true, new PaginationTotal(500, 50));

        var request = PaginationRequest.From(resultInfo, jumpTo: 6);

        request.PageNumber.Should().Be(6);
        request.PageSize.Should().BeSameAs(currentRequest.PageSize);
        request.PageCursor.Should().BeEquivalentTo(new PageCursor(5, resultInfo.FirstId, resultInfo.LastId, PageSortDirection.Descending));
    }

    [Theory]
    [DataInlineUnit(100L, 1L, 90L)]
    [DataInlineUnit(1L, 100L, 11L)]
    [DataInlineUnit(11L, 1L, 1L)]
    [DataInlineUnit(1L, 11L, 11L)]
    [DataInlineUnit(1L, long.MinValue, 1L)]
    [DataInlineUnit(long.MaxValue, long.MaxValue, long.MaxValue)]
    [DataInlineUnit(long.MaxValue, long.MaxValue - 1, long.MaxValue - 1)]
    [DataInlineUnit(long.MaxValue, 1L, long.MaxValue - 10)]
    [DataInlineUnit(long.MaxValue, long.MinValue, long.MaxValue - 10)]
    [DataInlineUnit(long.MaxValue - 9, long.MaxValue, long.MaxValue)]
    [DataInlineUnit(long.MaxValue - 10, long.MaxValue, long.MaxValue)]
    [DataInlineUnit(long.MaxValue - 11, long.MaxValue, long.MaxValue - 1)]
    public void From_Should_Limit_Page_Jump_In_Requested_Direction(long currentPage, long requestedPage, long expectedPage)
    {
        var currentRequest = new PaginationRequest(currentPage);
        var resultInfo = new PaginationResultInfo(currentRequest, Guid.NewGuid(), Guid.NewGuid(), 0, true, currentPage > 1, PaginationTotal.NotSpecified);

        var request = PaginationRequest.From(resultInfo, requestedPage);

        request.PageNumber.Should().Be(expectedPage);
        request.PageCursor.PageNumber.Should().Be(currentPage);
        request.IsPageRefresh.Should().Be(expectedPage == currentPage);
        request.NavigationDirection.Should().Be(expectedPage == currentPage
            ? PageNavigationDirection.Refresh
            : expectedPage > currentPage ? PageNavigationDirection.Next : PageNavigationDirection.Previous);
    }

    [Fact]
    public void PaginationRequest_Should_Be_Deserialized()
    {
        var request = PaginationRequest.Default;
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        request.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        request.PageCursor.Should().BeEquivalentTo(PageCursor.Initial);
        request.PageSize.Should().BeEquivalentTo(PageSize.Default);
        request.PageNumber.Should().Be(1);
        request.PageDifference.Should().Be(0);
        request.Total.Should().BeEquivalentTo(PaginationTotal.NotSpecified);
        request.UpdateTotalCount.Should().BeFalse();
        request.GetCursorId().Should().BeEmpty();
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();

        request = PaginationRequest.DefaultWith(50, 150, direction: PageSortDirection.Descending, updateTotalCount: true);
        request.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        request.PageCursor.Should().BeEquivalentTo(new PageCursor(1, Guid.Empty, Guid.Empty, PageSortDirection.Descending));
        request.PageSize.Should().BeEquivalentTo(new PageSize(50, 150));
        request.PageNumber.Should().Be(1);
        request.PageDifference.Should().Be(0);
        request.Total.Should().BeEquivalentTo(PaginationTotal.NotSpecified);
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
        request.PageCursor.Should().BeEquivalentTo(pageCursor);
        request.PageNumber.Should().Be(pageNumber);
        request.PageDifference.Should().Be(2);
        request.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount, pageSize.Size));
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
        request.PageSize.Should().BeEquivalentTo(pageSize);
        request.PageCursor.Should().BeEquivalentTo(pageCursor2);
        request.PageNumber.Should().Be(pageNumber);
        request.PageDifference.Should().Be(0);
        request.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount2, pageSize.Size));
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
        request.PageSize.Should().BeEquivalentTo(pageSize);
        request.PageCursor.Should().BeEquivalentTo(pageCursor3);
        request.PageNumber.Should().Be(pageNumber + 1);
        request.PageDifference.Should().Be(1);
        request.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount3, pageSize.Size));
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
        request.PageSize.Should().BeEquivalentTo(pageSize);
        request.PageCursor.Should().BeEquivalentTo(pageCursor4);
        request.PageNumber.Should().Be(pageNumber);
        request.PageDifference.Should().Be(1);
        request.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount3 + 1, pageSize.Size));
        request.UpdateTotalCount.Should().BeTrue();
        request.IsPageRefresh.Should().BeFalse();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        request.GetCursorId().Should().Be(firstId2);
        request.IsPageJump().Should().BeFalse();
        request.GetSkipSize().Should().Be(0);
        request.ValidateObjectSerialization();
    }
}
