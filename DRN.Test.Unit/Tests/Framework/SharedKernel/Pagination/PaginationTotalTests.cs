using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationTotalTests
{
    [Fact]
    public void PaginationTotal_Should_Be_Deserialized()
    {
        var total = new PaginationTotal(100, 25);
        total.Count.Should().Be(100);
        total.Pages.Should().Be(4);
        total.CountSpecified.Should().BeTrue();
        total.ValidateObjectSerialization();

        total = PaginationTotal.NotSpecified;
        total.Count.Should().Be(-1);
        total.Pages.Should().Be(-1);
        total.CountSpecified.Should().BeFalse();
        total.ValidateObjectSerialization();
    }
}