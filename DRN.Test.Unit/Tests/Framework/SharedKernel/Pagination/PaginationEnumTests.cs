using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationEnumTests
{
    [Fact]
    public void Pagination_Enums_Should_Have_Correct_Values()
    {
        ((byte)PageSortDirection.AscendingByCreatedAt).Should().Be(1);
        ((byte)PageSortDirection.DescendingByCreatedAt).Should().Be(2);
        
        ((byte)NavigationDirection.Next).Should().Be(1);
        ((byte)NavigationDirection.Previous).Should().Be(2);
        ((byte)NavigationDirection.Refresh).Should().Be(3);
    }
}