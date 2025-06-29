using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationTests
{
    [Fact]
    public void PageSize_Defaults()
    {
        var pageSize = PageSize.Default;
        pageSize.Size.Should().Be(10);
        pageSize.MaxSize.Should().Be(100);

        pageSize.ValidateObjectSerialization();
    }

    [Fact]
    public void PageSize_Default_Max_Size()
    {
        var pageSize = new PageSize(99);
        pageSize.Size.Should().Be(99);
        pageSize.MaxSize.Should().Be(100);

        pageSize.ValidateObjectSerialization();

        pageSize = new PageSize(101);
        pageSize.Size.Should().Be(100);
        pageSize.MaxSize.Should().Be(100);

        pageSize.ValidateObjectSerialization();
    }

    [Fact]
    public void PageSize_Invalid_Values()
    {
        var pageSize = new PageSize(0);
        pageSize.Size.Should().Be(1);
        pageSize.MaxSize.Should().Be(100);

        pageSize.ValidateObjectSerialization();

        pageSize = new PageSize(0, 0);
        pageSize.Size.Should().Be(1);
        pageSize.MaxSize.Should().Be(1);

        pageSize.ValidateObjectSerialization();
    }

    [Fact]
    public void PageSize_Custom_MaxSize()
    {
        var pageSize = new PageSize(50, 30);
        pageSize.Size.Should().Be(30);
        pageSize.MaxSize.Should().Be(30);

        pageSize.ValidateObjectSerialization();
    }

    [Fact]
    public void PageSize_Custom_MaxSize_With_Threshold()
    {
        var pageSize = new PageSize(150, 1001);
        pageSize.Size.Should().Be(150);
        pageSize.MaxSize.Should().Be(1000);

        pageSize.ValidateObjectSerialization();

        pageSize = new PageSize(1500, 1001);
        pageSize.Size.Should().Be(1000);
        pageSize.MaxSize.Should().Be(1000);

        pageSize.ValidateObjectSerialization();
    }

    [Fact]
    public void PageSize_MaxSize_Threshold_Override()
    {
        var pageSize = new PageSize(150, 1001, true);
        pageSize.Size.Should().Be(150);
        pageSize.MaxSize.Should().Be(1001);

        pageSize.ValidateObjectSerialization();

        pageSize = new PageSize(1500, 1001, true);
        pageSize.Size.Should().Be(1001);
        pageSize.MaxSize.Should().Be(1001);

        pageSize.ValidateObjectSerialization();
    }
}