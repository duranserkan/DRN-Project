using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PageCursorTests
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

        cursor.ValidateObjectSerialization();
    }

    [Theory]
    [InlineData(PageSortDirection.AscendingByCreatedAt)]
    [InlineData(PageSortDirection.DescendingByCreatedAt)]
    public void PageCursor_SecondRequest_Defaults(PageSortDirection direction)
    {
        var lastId = Guid.NewGuid();
        var cursor = new PageCursor(2, lastId, lastId, direction);
        cursor.IsFirstRequest.Should().BeFalse();
        cursor.IsFirstPage.Should().BeFalse();
        cursor.LastId.Should().Be(lastId);
        cursor.SortDirection.Should().Be(direction);

        cursor.ValidateObjectSerialization();
    }
}