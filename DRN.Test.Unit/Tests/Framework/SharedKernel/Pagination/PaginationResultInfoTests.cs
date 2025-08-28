using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationResultInfoTests
{
    [Fact]
    public void PaginationResultInfo_Should_Be_Deserialized()
    {
        var firstId = Guid.NewGuid();
        var lastId = Guid.NewGuid();
        var pageSize = new PageSize(7, 19);
        var pageCursor = new PageCursor(5, firstId, lastId, PageSortDirection.Descending);
        var pageNumber = 3;
        var totalCount = 41;
        var name1 = "1";
        var name2 = "2";
        var tag1 = new Tag(name1);
        var tag2 = new Tag(name2);
        IReadOnlyList<Tag> tags = [tag1, tag2];

        var request = new PaginationRequest(pageNumber, pageSize, pageCursor, totalCount, true, true);
        var result = new PaginationResult<Tag>(tags, request, totalCount);
        result.Request.Should().Be(request);
        result.Items.SequenceEqual(tags).Should().BeTrue();
        result.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount, pageSize.Size));
        result.HasNext.Should().BeTrue();
        result.HasPrevious.Should().BeTrue();
        result.ItemCount.Should().Be(2);
        result.Request.PageNumber.Should().Be(pageNumber);
        result.Request.PageSize.Should().BeEquivalentTo(pageSize);
        result.TotalCountUpdated.Should().BeTrue();

        var resultModel = result.ToModel(t => t.Name);
        resultModel.Info.Request.Should().Be(request);
        resultModel.Items.SequenceEqual([name1, name2]).Should().BeTrue();
        resultModel.Info.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount, pageSize.Size));
        resultModel.Info.HasNext.Should().BeTrue();
        resultModel.Info.HasPrevious.Should().BeTrue();
        resultModel.Info.ItemCount.Should().Be(2);
        resultModel.Info.Request.PageNumber.Should().Be(pageNumber);
        resultModel.Info.Request.PageSize.Should().BeEquivalentTo(pageSize);
        resultModel.Info.TotalCountUpdated.Should().BeTrue();
        resultModel.ValidateObjectSerialization();

        var info = result.ToResultInfo();
        info.Request.Should().Be(request);
        info.Total.Should().BeEquivalentTo(new PaginationTotal(totalCount, pageSize.Size));
        info.HasNext.Should().BeTrue();
        info.HasPrevious.Should().BeTrue();
        info.ItemCount.Should().Be(2);
        info.Request.PageNumber.Should().Be(pageNumber);
        info.Request.PageSize.Should().BeEquivalentTo(pageSize);
        info.TotalCountUpdated.Should().BeTrue();
        info.ValidateObjectSerialization();

        var resultJson = JsonSerializer.Serialize(result);
        var resultInfo2 = JsonSerializer.Deserialize<PaginationResultInfo>(resultJson);
        info.Should().BeEquivalentTo(resultInfo2);

        var resultInfoJson = JsonSerializer.Serialize(resultModel.Info);
        var resultInfo3 = JsonSerializer.Deserialize<PaginationResultInfo>(resultInfoJson);
        info.Should().BeEquivalentTo(resultInfo3);

        var totalCount2 = 132;
        var nextPage = info.RequestNextPage(true, totalCount2);
        nextPage.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        nextPage.PageNumber.Should().Be(4);
        nextPage.Total.Count.Should().Be(totalCount2);
        nextPage.UpdateTotalCount.Should().BeTrue();
        nextPage.IsPageJump().Should().BeFalse();

        var previousPage = info.RequestPreviousPage(true, totalCount2);
        previousPage.NavigationDirection.Should().Be(PageNavigationDirection.Previous);
        previousPage.PageNumber.Should().Be(2);
        previousPage.Total.Count.Should().Be(totalCount2);
        previousPage.UpdateTotalCount.Should().BeTrue();
        previousPage.IsPageJump().Should().BeFalse();

        var refreshPage = info.RequestRefresh(true, totalCount2);
        refreshPage.NavigationDirection.Should().Be(PageNavigationDirection.Refresh);
        refreshPage.PageNumber.Should().Be(3);
        refreshPage.Total.Count.Should().Be(totalCount2);
        refreshPage.UpdateTotalCount.Should().BeTrue();
        refreshPage.IsPageJump().Should().BeFalse();

        var firstPage = info.RequestPage(1, true, totalCount2);
        firstPage.NavigationDirection.Should().Be(PageNavigationDirection.Previous);
        firstPage.PageNumber.Should().Be(1);
        firstPage.Total.Count.Should().Be(totalCount2);
        firstPage.UpdateTotalCount.Should().BeTrue();
        firstPage.IsPageJump().Should().BeTrue();

        var sixthPage = info.RequestPage(6, true, totalCount2);
        sixthPage.NavigationDirection.Should().Be(PageNavigationDirection.Next);
        sixthPage.PageNumber.Should().Be(6);
        sixthPage.Total.Count.Should().Be(totalCount2);
        sixthPage.UpdateTotalCount.Should().BeTrue();
        sixthPage.IsPageJump().Should().BeTrue();
    }
}