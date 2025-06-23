using DRN.Framework.SharedKernel.Domain;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel;

public class PaginationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(PageSortDirection.AscendingByCreatedAt)]
    [InlineData(PageSortDirection.DescendingByCreatedAt)]
    public void PageCursor_FirstRequest_Defaults(PageSortDirection? direction)
    {
        var cursor = direction == null ? PageCursor.Initial : PageCursor.InitialWith(direction.Value);
        direction ??= PageSortDirection.AscendingByCreatedAt;

        cursor.IsFirstRequest.Should().BeTrue();
        cursor.IsFirstPage.Should().BeTrue();
        cursor.LastId.Should().Be(Guid.Empty);
        cursor.SortDirection.Should().Be(direction);
    }

    [Theory]
    [InlineData(PageSortDirection.AscendingByCreatedAt)]
    [InlineData(PageSortDirection.DescendingByCreatedAt)]
    public void PageCursor_SecondRequest_Defaults(PageSortDirection direction)
    {
        var lastId = Guid.NewGuid();
        var cursor = new PageCursor(2, lastId, direction);
        cursor.IsFirstRequest.Should().BeFalse();
        cursor.IsFirstPage.Should().BeFalse();
        cursor.LastId.Should().Be(lastId);
        cursor.SortDirection.Should().Be(direction);
    }

    [Fact]
    public void PageSize_Defaults()
    {
        var pageSize = PageSize.Default;
        pageSize.Size.Should().Be(10);
        pageSize.MaxSize.Should().Be(100);
    }

    [Fact]
    public void PageSize_Default_Max_Size()
    {
        var pageSize = new PageSize(99);
        pageSize.Size.Should().Be(99);
        pageSize.MaxSize.Should().Be(100);

        pageSize = new PageSize(101);
        pageSize.Size.Should().Be(100);
        pageSize.MaxSize.Should().Be(100);
    }

    [Fact]
    public void PageSize_Invalid_Values()
    {
        var pageSize = new PageSize(0);
        pageSize.Size.Should().Be(1);
        pageSize.MaxSize.Should().Be(100);

        pageSize = new PageSize(0, 0);
        pageSize.Size.Should().Be(1);
        pageSize.MaxSize.Should().Be(1);
    }

    [Fact]
    public void PageSize_Custom_MaxSize()
    {
        var pageSize = new PageSize(50, 30);
        pageSize.Size.Should().Be(30);
        pageSize.MaxSize.Should().Be(30);
    }

    [Fact]
    public void PageSize_Custom_MaxSize_With_Threshold()
    {
        var pageSize = new PageSize(150, 1001);
        pageSize.Size.Should().Be(150);
        pageSize.MaxSize.Should().Be(1000);

        pageSize = new PageSize(1500, 1001);
        pageSize.Size.Should().Be(1000);
        pageSize.MaxSize.Should().Be(1000);
    }

    [Fact]
    public void PageSize_MaxSize_Threshold_Override()
    {
        var pageSize = new PageSize(150, 1001, true);
        pageSize.Size.Should().Be(150);
        pageSize.MaxSize.Should().Be(1001);

        pageSize = new PageSize(1500, 1001, true);
        pageSize.Size.Should().Be(1001);
        pageSize.MaxSize.Should().Be(1001);
    }
}