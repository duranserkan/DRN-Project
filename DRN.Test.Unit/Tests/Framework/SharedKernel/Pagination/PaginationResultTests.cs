using DRN.Framework.SharedKernel.Domain.Pagination;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationResultTests
{
    [Theory]
    [DataInlineUnit(PageSortDirection.Ascending, 0, false)]
    [DataInlineUnit(PageSortDirection.Ascending, 2, false)]
    [DataInlineUnit(PageSortDirection.Ascending, 3, true)]
    [DataInlineUnit(PageSortDirection.Descending, 0, false)]
    [DataInlineUnit(PageSortDirection.Descending, 2, false)]
    [DataInlineUnit(PageSortDirection.Descending, 3, true)]
    public void Refresh_Should_Derive_HasNext_From_Excess_Items_Without_A_Total(
        PageSortDirection direction, int itemCount, bool expectedHasNext)
    {
        Tag[] availableItems = [new("First"), new("Second"), new("Third")];
        var items = availableItems.Take(itemCount).ToArray();
        var cursor = new PageCursor(2, Guid.NewGuid(), Guid.NewGuid(), direction);
        var request = new PaginationRequest(2, new PageSize(2), cursor);

        var result = new PaginationResult<Tag>(items, request);

        request.IsPageRefresh.Should().BeTrue();
        request.MarkAsHasNextOnRefresh.Should().BeFalse();
        result.HasNext.Should().Be(expectedHasNext);
        result.HasPrevious.Should().BeTrue();
        result.Total.CountSpecified.Should().BeFalse();
        result.ItemCount.Should().Be(Math.Min(itemCount, 2));
        result.Items.Should().Equal(items.Take(2));
        result.ToResultInfo().HasNext.Should().Be(expectedHasNext);
    }

    [Fact]
    public void Refresh_Should_Preserve_Explicit_Hints_And_Known_Total_Behavior()
    {
        Tag[] items = [new("First"), new("Second")];
        var cursor = new PageCursor(2, Guid.NewGuid(), Guid.NewGuid());
        var request = new PaginationRequest(2, new PageSize(2), cursor);
        var hintedRequest = new PaginationRequest(2, new PageSize(2), cursor, markAsHasNextOnRefresh: true);

        new PaginationResult<Tag>(items, hintedRequest).HasNext.Should().BeTrue();
        new PaginationResult<Tag>(items, request, totalCount: 5).HasNext.Should().BeTrue();
        new PaginationResult<Tag>(items, request, totalCount: 4).HasNext.Should().BeFalse();
    }
}
