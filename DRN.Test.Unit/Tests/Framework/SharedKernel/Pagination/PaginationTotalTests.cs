using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationTotalTests
{
    [Fact]
    public void PaginationTotal_Should_Be_Deserialized()
    {
        var total = new PaginationTotal(100, 25);

        total.ValidateObjectSerialization();
    }
}