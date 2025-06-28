using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Entity;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.EntityFramework;

public class PaginationUtilsTests
{
    //todo add tests to go same page
    [Theory]
    [DataInline(100, 5, true, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(100, 5, true, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(100, 5, false, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(100, 5, false, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(67, 10, true, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(67, 10, true, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(67, 10, false, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(67, 10, false, PageSortDirection.DescendingByCreatedAt)]
    public async Task PaginationUtils_Should_Return_Paginated_Result(TestContext context, int totalCount, int pageSize, bool updateTotalCount, PageSortDirection pageSortDirection)
    {
        _ = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
        var qaContext = context.GetRequiredService<QAContext>();
        var paginationUtils = context.GetRequiredService<IPaginationUtils>();

        var totalPageCount = (long)Math.Ceiling((decimal)totalCount / pageSize);
        var tagPrefix = $"{nameof(PaginationUtils_Should_Return_Paginated_Result)}_{Guid.NewGuid():N}";

        //Empty Page Result
        var tagQuery = qaContext.Tags.Where(t => t.Name.StartsWith(tagPrefix));
        var request = PaginationRequest.DefaultWith(pageSize, updateTotalCount, pageSortDirection);
        var paginationResult = await paginationUtils.ToPaginationResultAsync(tagQuery, request);
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
        request = PaginationRequest.DefaultWith(pageSize, updateTotalCount, pageSortDirection);
        expectedPages.ValidateFirstRequest(request);

        paginationResult = await paginationUtils.ToPaginationResultAsync(tagQuery, request);
        expectedPages.ValidatePageResult(request, paginationResult);

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

            nextPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, nextPageRequest);
            expectedPages.ValidatePageResult(nextPageRequest, nextPageResult);
        }

        //Paginate Backward
        var cursor = new PageCursor(nextPageResult.PageNumber, nextPageResult.FirstId, nextPageResult.LastId, nextPageRequest.PageCursor.SortDirection);
        var previousPageRequest = new PaginationRequest(nextPageResult.PageNumber - 1, cursor, nextPageRequest.PageSize, updateTotalCount);
        expectedPages.ValidateRequest(previousPageRequest, totalPageCount, updateTotalCount);

        var previousPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, previousPageRequest);
        expectedPages.ValidatePageResult(previousPageRequest, previousPageResult);

        var remainingPages = totalPageCount - 1;
        for (var i = 1; i < remainingPages; i++)
        {
            //following requests are created from result since request chain is started
            var previousPageNumber = previousPageRequest.PageNumber;
            previousPageRequest = previousPageResult.RequestPreviousPage();
            expectedPages.ValidateRequest(previousPageRequest, previousPageNumber, false);

            previousPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, previousPageRequest);
            expectedPages.ValidatePageResult(previousPageRequest, previousPageResult);
        }

        //Page jump to last page
        var preJumpPageNumber = previousPageRequest.PageNumber;
        var lastPageRequest = previousPageResult.RequestPageJumpTo(totalPageCount);
        expectedPages.ValidateRequest(lastPageRequest, preJumpPageNumber, false, true, (int)totalPageCount - 1);

        var lastPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, lastPageRequest);
        expectedPages.ValidatePageResult(lastPageRequest, lastPageResult);

        //Page jump to First Page
        preJumpPageNumber = lastPageResult.PageNumber;
        var firstPageRequest = lastPageResult.RequestPageJumpTo(1);
        expectedPages.ValidateRequest(firstPageRequest, preJumpPageNumber, false, true, (int)totalPageCount - 1);

        var firstPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, firstPageRequest);
        expectedPages.ValidatePageResult(firstPageRequest, firstPageResult);

        //Page jump to Page 4
        preJumpPageNumber = firstPageResult.PageNumber;
        var request4 = firstPageResult.RequestPageJumpTo(4);
        expectedPages.ValidateRequest(request4, preJumpPageNumber, false, true, 3);

        var pageResult4 = await paginationUtils.ToPaginationResultAsync(tagQuery, request4);
        expectedPages.ValidatePageResult(request4, pageResult4);

        //Page jump to Page 2
        preJumpPageNumber = pageResult4.PageNumber;
        var request2 = pageResult4.RequestPageJumpTo(2);
        expectedPages.ValidateRequest(request2, preJumpPageNumber, false, true, 2);

        var pageResult2 = await paginationUtils.ToPaginationResultAsync(tagQuery, request2);
        expectedPages.ValidatePageResult(request2, pageResult2);
    }

    public record ExpectedPageResultCollection(Tag[] Tags, int TotalCount, int PageSize, bool UpdateTotalCount, PageSortDirection PageSortDirection)
    {
        public long TotalPageCount => (long)Math.Ceiling((decimal)TotalCount / PageSize);

        public ExpectedPageResult[] ExpectedPageResults { get; } = PageSortDirection == PageSortDirection.AscendingByCreatedAt
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
                ? NavigationDirection.Next
                : NavigationDirection.Previous;
            expectedNavigationDirection = request.PageNumber == cursor.PageNumber
                ? NavigationDirection.Same
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

        public void ValidatePageResult(PaginationRequest request, PaginationResult<Tag> result)
        {
            var expectedPage = GetPage(request.PageNumber);

            result.FirstId.Should().Be(expectedPage.FirstId);
            result.LastId.Should().Be(expectedPage.LastId);
            result.Items.SequenceEqual(expectedPage.Tags).Should().BeTrue();

            var resultIndexes = result.Items.Select(t => t.Model.Other).ToArray();
            resultIndexes.SequenceEqual(expectedPage.Indexes).Should().BeTrue();

            result.HasNext.Should().Be(expectedPage.PageNumber != TotalPageCount);
            result.HasPrevious.Should().Be(expectedPage.PageNumber != 1);

            var expectedCount = Tags.Skip((int)(request.PageNumber - 1) * PageSize).Take(PageSize).ToArray().Length;
            result.Items.Count.Should().Be(expectedCount);
            result.Items.Count.Should().BePositive();

            result.TotalCountSpecified.Should().Be(request.UpdateTotalCount);
            result.TotalPages.Should().Be(request.UpdateTotalCount ? TotalPageCount : -1);
            result.TotalCount.Should().Be(request.UpdateTotalCount ? TotalCount : -1);
            result.PageSize.Should().Be(PageSize);
            result.PageNumber.Should().Be(expectedPage.PageNumber);

            var isLastPage = result.PageNumber == TotalPageCount;
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
}