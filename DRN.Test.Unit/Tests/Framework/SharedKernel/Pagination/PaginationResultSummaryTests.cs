using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Unit.Tests.Framework.SharedKernel.Pagination;

public class PaginationResultSummaryTests
{
    [Fact]
    public void PaginationResultSummary_Should_Be_Deserialized()
    {
        var firstId = Guid.NewGuid();
        var lastId = Guid.NewGuid();
        var pageSize = new PageSize(7, 19);
        var pageCursor = new PageCursor(5, firstId, lastId, PageSortDirection.DescendingByCreatedAt);
        var pageNumber = 3;
        var totalCount = 41;
        var name1 = "1";
        var name2 = "2";
        var tag1 = new Tag(name1);
        var tag2 = new Tag(name2);
        IReadOnlyList<Tag> tags = [tag1, tag2];

        var request = new PaginationRequest(pageNumber, pageCursor, pageSize, true, totalCount, true);
        var result = new PaginationResult<Tag>(tags, request, totalCount);
        result.Request.Should().Be(request);
        result.Items.SequenceEqual(tags).Should().BeTrue();
        result.Total.Should().Be(new PaginationTotal(totalCount, pageSize.Size));
        result.HasNext.Should().BeTrue();
        result.HasPrevious.Should().BeTrue();
        result.ItemCount.Should().Be(2);
        result.PageNumber.Should().Be(pageNumber);
        result.PageSize.Should().Be(pageSize.Size);
        result.TotalCountUpdated.Should().BeTrue();

        var resultModel = result.ToModel(t => t.Name);
        resultModel.Request.Should().Be(request);
        resultModel.Items.SequenceEqual([name1, name2]).Should().BeTrue();
        resultModel.Total.Should().Be(new PaginationTotal(totalCount, pageSize.Size));
        resultModel.HasNext.Should().BeTrue();
        resultModel.HasPrevious.Should().BeTrue();
        resultModel.ItemCount.Should().Be(2);
        resultModel.PageNumber.Should().Be(pageNumber);
        resultModel.PageSize.Should().Be(pageSize.Size);
        resultModel.TotalCountUpdated.Should().BeTrue();

        var resultSummary = result.ToSummary();
        resultSummary.Request.Should().Be(request);
        resultSummary.Total.Should().Be(new PaginationTotal(totalCount, pageSize.Size));
        resultSummary.HasNext.Should().BeTrue();
        resultSummary.HasPrevious.Should().BeTrue();
        resultSummary.ItemCount.Should().Be(2);
        resultSummary.PageNumber.Should().Be(pageNumber);
        resultSummary.PageSize.Should().Be(pageSize.Size);
        resultSummary.TotalCountUpdated.Should().BeTrue();
        resultSummary.ValidateObjectSerialization();

        var resultJson = JsonSerializer.Serialize(result);
        var resultSummary2 = JsonSerializer.Deserialize<PaginationResultSummary>(resultJson);
        resultSummary.Should().BeEquivalentTo(resultSummary2);

        var resultJson2 = JsonSerializer.Serialize(resultModel);
        var resultSummary3 = JsonSerializer.Deserialize<PaginationResultSummary>(resultJson2);
        resultSummary.Should().BeEquivalentTo(resultSummary3);


        var totalCount2 = 132;
        var nextPage = resultSummary.RequestNextPage(true, totalCount2);
        nextPage.NavigationDirection.Should().Be(NavigationDirection.Next);
        nextPage.PageNumber.Should().Be(4);
        nextPage.Total.Count.Should().Be(totalCount2);
        nextPage.UpdateTotalCount.Should().BeTrue();
        nextPage.IsPageJump().Should().BeFalse();
        
        var previousPage = resultSummary.RequestPreviousPage(true, totalCount2);
        previousPage.NavigationDirection.Should().Be(NavigationDirection.Previous);
        previousPage.PageNumber.Should().Be(2);
        previousPage.Total.Count.Should().Be(totalCount2);
        previousPage.UpdateTotalCount.Should().BeTrue();
        previousPage.IsPageJump().Should().BeFalse();
        
        var refreshPage = resultSummary.RequestRefresh(true, totalCount2);
        refreshPage.NavigationDirection.Should().Be(NavigationDirection.Refresh);
        refreshPage.PageNumber.Should().Be(3);
        refreshPage.Total.Count.Should().Be(totalCount2);
        refreshPage.UpdateTotalCount.Should().BeTrue();
        refreshPage.IsPageJump().Should().BeFalse();
        
        var firstPage = resultSummary.RequestPage(1, true, totalCount2);
        firstPage.NavigationDirection.Should().Be(NavigationDirection.Previous);
        firstPage.PageNumber.Should().Be(1);
        firstPage.Total.Count.Should().Be(totalCount2);
        firstPage.UpdateTotalCount.Should().BeTrue();
        firstPage.IsPageJump().Should().BeTrue();
        
        var sixthPage = resultSummary.RequestPage(6, true, totalCount2);
        sixthPage.NavigationDirection.Should().Be(NavigationDirection.Next);
        sixthPage.PageNumber.Should().Be(6);
        sixthPage.Total.Count.Should().Be(totalCount2);
        sixthPage.UpdateTotalCount.Should().BeTrue();
        sixthPage.IsPageJump().Should().BeTrue();
    }
}