using System.Text.Json;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Testing.Extensions;
using DRN.Framework.Utils.Entity;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.Utils;

public class PaginationUtilsTests
{
    [Theory]
    [DataInline(90, 5, true, PageSortDirection.Ascending)]
    [DataInline(90, 5, true, PageSortDirection.Descending)]
    [DataInline(90, 5, false, PageSortDirection.Ascending)]
    [DataInline(90, 5, false, PageSortDirection.Descending)]
    [DataInline(67, 10, true, PageSortDirection.Ascending)]
    [DataInline(67, 10, true, PageSortDirection.Descending)]
    [DataInline(67, 10, false, PageSortDirection.Ascending)]
    [DataInline(67, 10, false, PageSortDirection.Descending)]
    public async Task PaginationUtils_Should_Return_Paginated_Result(TestContext context, int totalCount, int pageSize, bool updateTotalCount, PageSortDirection pageSortDirection)
    {
        _ = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
        var qaContext = context.GetRequiredService<QAContext>();
        var paginationUtils = context.GetRequiredService<IPaginationUtils>();

        var totalPageCount = (long)Math.Ceiling((decimal)totalCount / pageSize);
        var tagPrefix = $"{nameof(PaginationUtils_Should_Return_Paginated_Result)}_{Guid.NewGuid():N}";

        //Empty Page Result
        var tagQuery = qaContext.Tags.Where(t => t.Name.StartsWith(tagPrefix));
        var request = PaginationRequest.DefaultWith(pageSize, PageSize.MaxSizeDefault, updateTotalCount, pageSortDirection);
        var paginationResult = await paginationUtils.GetResultAsync(tagQuery, request);
        paginationResult.Items.Count.Should().Be(0);
        request.PageCursor.PageNumber.Should().Be(1);
        request.PageCursor.IsFirstPage.Should().BeTrue();
        request.PageCursor.IsFirstRequest.Should().BeTrue();
        request.UpdateTotalCount.Should().Be(updateTotalCount);
        request.PageSize.Size.Should().Be(pageSize);

        //Create to be paginated entities
        var itemIndexes = Enumerable.Range(0, totalCount).ToArray();
        var tags = itemIndexes.Select(index => new Tag($"{tagPrefix}_{index}") { Model = new TagValueModel { Other = index } }).ToArray();
        await qaContext.Tags.AddRangeAsync(tags);
        await qaContext.SaveChangesAsync();

        //Initial Page Result
        var expectedPages = new ExpectedPageResultCollection(tags, totalCount, pageSize, updateTotalCount, pageSortDirection);
        request = PaginationRequest.DefaultWith(pageSize, PageSize.MaxSizeDefault, updateTotalCount, pageSortDirection);
        expectedPages.ValidateFirstRequest(request);

        paginationResult = await paginationUtils.GetResultAsync(tagQuery, request);
        expectedPages.ValidateResult(paginationResult, updateTotalCount);
        paginationResult.TotalCountUpdated.Should().Be(updateTotalCount);

        //Remaining Pages
        var nextPageRequest = request;
        var nextPageResult = paginationResult;

        //Paginate Forward
        for (var i = 1; i < totalPageCount; i++)
        {
            //following requests are created from result since request chain is started
            var previousPageNumber = nextPageRequest.PageNumber;
            nextPageRequest = nextPageResult.RequestNextPage();
            expectedPages.ValidateRequest(nextPageRequest, previousPageNumber, false);

            nextPageResult = await paginationUtils.GetResultAsync(tagQuery, nextPageRequest);
            expectedPages.ValidateResult(nextPageResult, updateTotalCount);
            nextPageResult.TotalCountUpdated.Should().Be(false);
        }

        //Paginate Backward
        var cursor = new PageCursor(nextPageResult.Request.PageNumber, nextPageResult.FirstId, nextPageResult.LastId, nextPageRequest.PageCursor.SortDirection);
        var previousPageRequest = new PaginationRequest(nextPageResult.Request.PageNumber - 1, nextPageRequest.PageSize, cursor, nextPageResult.Total.Count, updateTotalCount);
        expectedPages.ValidateRequest(previousPageRequest, totalPageCount, updateTotalCount);

        var previousPageResult = await paginationUtils.GetResultAsync(tagQuery, previousPageRequest);
        expectedPages.ValidateResult(previousPageResult, updateTotalCount);

        var remainingPages = totalPageCount - 1;
        for (var i = 1; i < remainingPages; i++)
        {
            //following requests are created from result since request chain is started
            var previousPageNumber = previousPageRequest.PageNumber;
            previousPageRequest = previousPageResult.RequestPreviousPage();
            expectedPages.ValidateRequest(previousPageRequest, previousPageNumber, false);

            previousPageResult = await paginationUtils.GetResultAsync(tagQuery, previousPageRequest);
            expectedPages.ValidateResult(previousPageResult, updateTotalCount);
            previousPageResult.TotalCountUpdated.Should().Be(false);
        }

        //Page jump to last page
        var preJumpPageNumber = previousPageRequest.PageNumber;
        var lastPageRequest = previousPageResult.RequestPage(totalPageCount);
        expectedPages.ValidateRequest(lastPageRequest, preJumpPageNumber, false, true, (int)totalPageCount - 1);

        var lastPageResult = await paginationUtils.GetResultAsync(tagQuery, lastPageRequest);
        expectedPages.ValidateResult(lastPageResult, updateTotalCount);
        lastPageResult.TotalCountUpdated.Should().Be(false);

        //test refresh on the last page
        var lastPageRefreshRequest = lastPageResult.RequestRefresh();
        expectedPages.ValidateRequest(lastPageRefreshRequest, lastPageResult.Request.PageNumber, false, false, 0);

        var lastPageRefreshResult = await paginationUtils.GetResultAsync(tagQuery, lastPageRefreshRequest);
        expectedPages.ValidateResult(lastPageRefreshResult, updateTotalCount);
        lastPageRefreshResult.TotalCountUpdated.Should().Be(false);

        //Page jump to First Page
        preJumpPageNumber = lastPageResult.Request.PageNumber;
        var firstPageRequest = lastPageResult.RequestPage(1);
        expectedPages.ValidateRequest(firstPageRequest, preJumpPageNumber, false, true, (int)totalPageCount - 1);

        var firstPageResult = await paginationUtils.GetResultAsync(tagQuery, firstPageRequest);
        expectedPages.ValidateResult(firstPageResult, updateTotalCount);
        firstPageResult.TotalCountUpdated.Should().Be(false);

        //test refresh on the first page
        var firstPageRefreshRequest = firstPageResult.RequestRefresh();
        expectedPages.ValidateRequest(firstPageRefreshRequest, 1, false, false, 0);

        var firstPageRefreshResult = await paginationUtils.GetResultAsync(tagQuery, firstPageRequest);
        expectedPages.ValidateResult(firstPageRefreshResult, updateTotalCount);
        firstPageRefreshResult.TotalCountUpdated.Should().Be(false);

        //Page jump to Page 4
        preJumpPageNumber = firstPageResult.Request.PageNumber;
        var request4 = firstPageResult.RequestPage(4);
        expectedPages.ValidateRequest(request4, preJumpPageNumber, false, true, 3);

        var pageResult4 = await paginationUtils.GetResultAsync(tagQuery, request4);
        expectedPages.ValidateResult(pageResult4, updateTotalCount);
        pageResult4.TotalCountUpdated.Should().Be(false);

        //Page jump to Page 2
        preJumpPageNumber = pageResult4.Request.PageNumber;
        var request2 = pageResult4.RequestPage(2);
        expectedPages.ValidateRequest(request2, preJumpPageNumber, false, true, 2);

        var pageResult2 = await paginationUtils.GetResultAsync(tagQuery, request2);
        expectedPages.ValidateResult(pageResult2, updateTotalCount);
        pageResult2.TotalCountUpdated.Should().Be(false);

        //refresh Page 2
        request2 = pageResult2.RequestRefresh();
        expectedPages.ValidateRequest(request2, 2, false);
        request2.MarkAsHasNextOnRefresh.Should().Be(pageResult2.HasNext);

        pageResult2 = await paginationUtils.GetResultAsync(tagQuery, request2);
        expectedPages.ValidateResult(pageResult2, updateTotalCount);
        pageResult2.TotalCountUpdated.Should().Be(false);

        //jump to page 100 to test the empty page
        var request100 = pageResult2.RequestPage(100);
        expectedPages.ValidateRequest(request100, pageResult2.Request.PageNumber, false, true, 98);

        var pageResult100 = await paginationUtils.GetResultAsync(tagQuery, request100);
        pageResult100.Items.Should().BeEmpty();
        pageResult100.HasPrevious.Should().BeTrue();
        pageResult100.HasNext.Should().BeFalse();
        pageResult100.FirstId.Should().BeEmpty();
        pageResult100.LastId.Should().BeEmpty();
        pageResult100.Total.Should().BeEquivalentTo(pageResult2.Total);
        pageResult100.TotalCountUpdated.Should().Be(false);
    }
}

public record ExpectedPageResultCollection(Tag[] Tags, int TotalCount, int PageSize, bool UpdateTotalCount, PageSortDirection PageSortDirection)
{
    public long TotalPageCount => (long)Math.Ceiling((decimal)TotalCount / PageSize);

    public ExpectedPageResult[] ExpectedPageResults { get; } = PageSortDirection == PageSortDirection.Ascending
        ? Tags.Order().Chunk(PageSize).Select((tags, index) => new ExpectedPageResult(tags, index + 1, PageSize)).ToArray()
        : Tags.OrderDescending().Chunk(PageSize).Select((tags, index) => new ExpectedPageResult(tags, index + 1, PageSize)).ToArray();

    public ExpectedPageResult GetPage(long page) => ExpectedPageResults[page - 1];
    public ExpectedPageResult GetLastPage() => ExpectedPageResults[^1];
    public ExpectedPageResult GetFirstPage() => ExpectedPageResults[0];

    public void ValidateFirstRequest(PaginationRequest request)
    {
        var cursor = request.PageCursor;
        cursor.FirstId.Should().Be(Guid.Empty);
        cursor.LastId.Should().Be(Guid.Empty);
        cursor.PageNumber.Should().Be(1);
        cursor.IsFirstPage.Should().BeTrue();
        cursor.IsFirstRequest.Should().BeTrue();

        request.UpdateTotalCount.Should().Be(UpdateTotalCount);
        request.PageSize.Size.Should().Be(PageSize);
    }

    public void ValidateRequest(PaginationRequest request, long previousPageNumber, bool updateTotalCount, bool pageJump = false, int pageDifference = 0)
    {
        var previousPage = GetPage(previousPageNumber);

        var cursor = request.PageCursor;
        cursor.FirstId.Should().Be(previousPage.FirstId);
        cursor.LastId.Should().Be(previousPage.LastId);
        cursor.PageNumber.Should().Be(previousPage.PageNumber);
        cursor.IsFirstPage.Should().Be(previousPage.PageNumber == 1);
        cursor.IsFirstRequest.Should().BeFalse();

        var expectedNavigationDirection = request.PageNumber > cursor.PageNumber
            ? PageNavigationDirection.Next
            : PageNavigationDirection.Previous;
        expectedNavigationDirection = request.PageNumber == cursor.PageNumber
            ? PageNavigationDirection.Refresh
            : expectedNavigationDirection;

        request.NavigationDirection.Should().Be(expectedNavigationDirection);
        request.PageSize.Size.Should().Be(PageSize);
        request.UpdateTotalCount.Should().Be(updateTotalCount);

        request.IsPageJump().Should().Be(pageJump);
        if (pageJump)
            ValidatePageJump(request, pageDifference);
    }

    public void ValidatePageJump(PaginationRequest request, int pageDifference)
    {
        request.PageDifference.Should().Be(pageDifference);
        request.GetSkipSize().Should().Be((pageDifference - 1) * PageSize);
        request.IsPageJump().Should().BeTrue();
    }

    public void ValidateResult(PaginationResult<Tag> result, bool updateTotalCount)
    {
        var request = result.Request;
        var expectedPage = GetPage(request.PageNumber);

        result.FirstId.Should().Be(expectedPage.FirstId);
        result.LastId.Should().Be(expectedPage.LastId);
        result.Items.SequenceEqual(expectedPage.Tags).Should().BeTrue();

        var resultIndexes = result.Items.Select(t => t.Model.Other).ToArray();
        resultIndexes.SequenceEqual(expectedPage.Indexes).Should().BeTrue();

        result.HasNext.Should().Be(expectedPage.PageNumber != TotalPageCount);
        result.HasPrevious.Should().Be(expectedPage.PageNumber != 1);

        var expectedCount = Tags.Skip((int)(request.PageNumber - 1) * PageSize).Take(PageSize).ToArray().Length;
        result.ItemCount.Should().Be(expectedCount);
        result.Items.Count.Should().Be(expectedCount);
        result.Items.Count.Should().BePositive();

        result.Total.CountSpecified.Should().Be(updateTotalCount);
        result.Total.Pages.Should().Be(updateTotalCount ? TotalPageCount : -1);
        result.Total.Count.Should().Be(updateTotalCount ? TotalCount : -1);
        result.Request.PageSize.Size.Should().Be(PageSize);
        result.Request.PageNumber.Should().Be(expectedPage.PageNumber);

        var isLastPage = result.Request.PageNumber == TotalPageCount;
        if (isLastPage)
        {
            result.HasNext.Should().BeFalse();
            result.GetTotalCountUpToCurrentPage().Should().Be(TotalCount);
        }
        else
        {
            result.HasNext.Should().BeTrue();
            result.GetTotalCountUpToCurrentPage().Should().Be(request.PageNumber * PageSize);
        }

        if (!request.PageCursor.IsFirstRequest && request.PageNumber == request.PageCursor.PageNumber)
            result.Request.IsPageRefresh.Should().BeTrue();

        var resultModel = result.ToModel(y => y.Model.Other);

        resultModel.Items.Count.Should().Be(result.Items.Count);
        resultModel.Items.SequenceEqual(resultIndexes).Should().BeTrue();

        var resultInfo = result.ToResultInfo();
        resultInfo.ValidateObjectSerialization();
        request.ValidateObjectSerialization();

        var resultJson = JsonSerializer.Serialize(result);
        var resultModelInfoJson = JsonSerializer.Serialize(resultModel.Info);
        var resultInfoFromResultJson = JsonSerializer.Deserialize<PaginationResultInfo>(resultJson);
        var resultInfoFromResultModelInfoJson = JsonSerializer.Deserialize<PaginationResultInfo>(resultModelInfoJson);
        resultInfo.Should().BeEquivalentTo(resultInfoFromResultJson);
        resultInfo.Should().BeEquivalentTo(resultInfoFromResultModelInfoJson);
    }
}

public record ExpectedPageResult(Tag[] Tags, long PageNumber, long PageSize)
{
    public long[] Indexes => Tags.Select(t => t.Model.Other).ToArray();
    public Tag FirstTag => Tags[0];
    public Guid FirstId => Tags[0].EntityId;
    public long FirstIndex => Indexes[0];
    public Tag LastTag => Tags[^1];
    public Guid LastId => Tags[^1].EntityId;
    public long LastIndex => Indexes[^1];
}