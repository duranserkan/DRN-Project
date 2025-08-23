using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationEnumTests
{
    [Fact]
    public void Pagination_Enums_Should_Have_Correct_Values()
    {
        ((byte)PageSortDirection.Ascending).Should().Be(1);
        ((byte)PageSortDirection.Descending).Should().Be(2);
        
        ((byte)PageNavigationDirection.Next).Should().Be(1);
        ((byte)PageNavigationDirection.Previous).Should().Be(2);
        ((byte)PageNavigationDirection.Refresh).Should().Be(3);
    }
}