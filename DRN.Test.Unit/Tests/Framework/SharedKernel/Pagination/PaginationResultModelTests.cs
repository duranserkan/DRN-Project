using DRN.Framework.SharedKernel.Domain.Pagination;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationResultModelTests
{
    [Fact]
    public void ToModel_Should_Map_Items_In_Order_And_Preserve_Exact_Info_Instance()
    {
        var info = new PaginationResultInfo(
            new PaginationRequest(1, new PageSize(10, 20), null, 3, true, true),
            firstId: Guid.Empty,
            lastId: Guid.Empty,
            itemCount: 3,
            hasNext: false,
            hasPrevious: false,
            total: new PaginationTotal(3, 10)
        );

        var originalItems = new[] { 10, 20, 30 };
        var model = new PaginationResultModel<int>(info, originalItems);

        var mappedCalls = 0;
        var resultModel = model.ToModel(x =>
        {
            mappedCalls++;
            return $"val_{x}";
        });

        mappedCalls.Should().Be(3);
        resultModel.Items.Should().Equal("val_10", "val_20", "val_30");
        resultModel.Info.Should().BeSameAs(info);
        resultModel.Info.Total.Count.Should().Be(3);
        resultModel.Info.ItemCount.Should().Be(3);
    }

    [Fact]
    public void ToModel_Should_Produce_Empty_Result_Without_Invoking_Mapper_When_Items_Are_Empty()
    {
        var info = new PaginationResultInfo(
            new PaginationRequest(1, new PageSize(10, 20), null, 0, false, false),
            firstId: Guid.Empty,
            lastId: Guid.Empty,
            itemCount: 0,
            hasNext: false,
            hasPrevious: false,
            total: new PaginationTotal(0, 10)
        );

        var model = new PaginationResultModel<int>(info, Array.Empty<int>());
        var mapperInvoked = false;

        var resultModel = model.ToModel(x =>
        {
            mapperInvoked = true;
            return x.ToString();
        });

        mapperInvoked.Should().BeFalse();
        resultModel.Items.Should().BeEmpty();
        resultModel.Info.Should().BeSameAs(info);
    }

    [Fact]
    public void ToModel_Should_Propagate_Mapper_Exception()
    {
        var info = new PaginationResultInfo(
            new PaginationRequest(1, new PageSize(10, 20), null, 1, false, false),
            firstId: Guid.Empty,
            lastId: Guid.Empty,
            itemCount: 1,
            hasNext: false,
            hasPrevious: false,
            total: new PaginationTotal(1, 10)
        );

        var model = new PaginationResultModel<int>(info, [1]);
        var expectedException = new InvalidOperationException("Mapping failure");

        var act = () => model.ToModel<string>(_ => throw expectedException);

        act.Should().ThrowExactly<InvalidOperationException>()
            .Which.Should().BeSameAs(expectedException);
    }
}
