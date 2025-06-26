using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Entity;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.EntityFramework;

public class PaginationUtilsTests
{
    //todo add tests to jump forward page
    //todo add tests to jump previous page
    //todo add tests to go same page
    [Theory]
    [DataInline(100, 5, true, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(100, 5, true, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(100, 5, false, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(100, 5, false, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(65, 20, true, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(65, 20, true, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(65, 20, false, PageSortDirection.AscendingByCreatedAt)]
    [DataInline(65, 20, false, PageSortDirection.DescendingByCreatedAt)]
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

        //Initial Page Result
        var itemIndexes = Enumerable.Range(0, totalCount).ToArray();
        var tags = itemIndexes.Select(index => new Tag($"{tagPrefix}_{index}") { Model = new TagValueModel { Other = index } }).ToArray();
        await qaContext.Tags.AddRangeAsync(tags);
        await qaContext.SaveChangesAsync();

        request = PaginationRequest.DefaultWith(pageSize, updateTotalCount, pageSortDirection);
        request.PageCursor.FirstId.Should().Be(Guid.Empty);
        request.PageCursor.LastId.Should().Be(Guid.Empty);
        request.PageCursor.PageNumber.Should().Be(1);
        request.PageCursor.IsFirstRequest.Should().BeTrue();
        request.UpdateTotalCount.Should().Be(updateTotalCount);
        request.PageSize.Size.Should().Be(pageSize);

        paginationResult = await paginationUtils.ToPaginationResultAsync(tagQuery, request);
        paginationResult.FirstId.Should().Be(paginationResult.Items[0].EntityId);
        paginationResult.LastId.Should().Be(paginationResult.Items[^1].EntityId);

        paginationResult.HasNext.Should().BeTrue();
        paginationResult.HasPrevious.Should().BeFalse();

        paginationResult.Items.Count.Should().Be(pageSize);
        paginationResult.TotalCountSpecified.Should().Be(updateTotalCount);
        paginationResult.TotalPages.Should().Be(updateTotalCount ? totalPageCount : -1);
        paginationResult.TotalCount.Should().Be(updateTotalCount ? totalCount : -1);

        //Remaining Pages
        var remainingPages = totalPageCount - 1;
        var nextPageRequest = request;
        var nextPageResult = paginationResult;

        var forwardIndexChunks = pageSortDirection == PageSortDirection.AscendingByCreatedAt
            ? itemIndexes.Chunk(pageSize).ToArray()
            : itemIndexes.OrderDescending().Chunk(pageSize).ToArray();

        var forwardIndexChunk = forwardIndexChunks[0];
        var forwardResultIndexes = nextPageResult.Items.Select(tag => tag.Model.Other).ToArray();
        forwardResultIndexes.Should().BeEquivalentTo(forwardIndexChunk);

        //Paginate Forward
        for (var i = 1; i < totalPageCount; i++)
        {
            //following requests are created from result since request chain is started
            nextPageRequest = nextPageResult.GetNextPage(nextPageRequest);
            if (i == 1) //When next request is made for page 2 then cursor shows the previous page which is the first page
                nextPageRequest.PageCursor.IsFirstPage.Should().BeTrue();
            else
                nextPageRequest.PageCursor.IsFirstPage.Should().BeFalse();

            nextPageRequest.PageCursor.IsFirstRequest.Should().BeFalse();
            nextPageRequest.PageCursor.FirstId.Should().Be(nextPageResult.FirstId);
            nextPageRequest.PageCursor.LastId.Should().Be(nextPageResult.LastId);

            nextPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, nextPageRequest);
            nextPageResult.HasPrevious.Should().BeTrue();

            var expectedPageNumber = i + 1;
            var previousItems = (expectedPageNumber - 1) * pageSize;
            var expectedIndexes = itemIndexes.Skip(previousItems).Take(pageSize).ToArray();
            var expectedCount = expectedIndexes.Length;

            nextPageResult.PageSize.Should().Be(pageSize);
            nextPageResult.PageNumber.Should().Be(expectedPageNumber);
            nextPageResult.Items.Count.Should().BePositive();
            nextPageResult.Items.Count.Should().Be(expectedCount);
            nextPageResult.FirstId.Should().Be(nextPageResult.Items[0].EntityId);
            nextPageResult.LastId.Should().Be(nextPageResult.Items[^1].EntityId);
            nextPageResult.TotalCountSpecified.Should().BeFalse();
            nextPageResult.TotalCount.Should().Be(-1);
            nextPageResult.TotalPages.Should().Be(-1);

            var isLastPage = nextPageResult.PageNumber == totalPageCount;
            if (isLastPage)
            {
                nextPageResult.HasNext.Should().BeFalse();
                nextPageResult.GetTotalCountUpToCurrentPage().Should().Be(totalCount);
            }
            else
            {
                nextPageResult.HasNext.Should().BeTrue();
                nextPageResult.GetTotalCountUpToCurrentPage().Should().Be(nextPageRequest.PageNumber * pageSize);
            }

            forwardIndexChunk = forwardIndexChunks[i];
            forwardResultIndexes = nextPageResult.Items.Select(tag => tag.Model.Other).ToArray();
            forwardResultIndexes.Should().BeEquivalentTo(forwardIndexChunk);
        }
        
        //Paginate Backward
        var backwardIndexChunks = forwardIndexChunks.Reverse().Skip(1).ToArray();
        var cursor = new PageCursor(nextPageResult.PageNumber, nextPageResult.FirstId, nextPageResult.LastId, nextPageRequest.PageCursor.SortDirection);
        var previousPageRequest = new PaginationRequest(nextPageResult.PageNumber - 1, cursor, nextPageRequest.PageSize, updateTotalCount);
        previousPageRequest.PageCursor.FirstId.Should().Be(nextPageResult.FirstId);
        previousPageRequest.PageCursor.LastId.Should().Be(nextPageResult.LastId);

        previousPageRequest.PageCursor.PageNumber.Should().Be(totalPageCount);
        previousPageRequest.PageCursor.IsFirstRequest.Should().BeFalse();
        previousPageRequest.UpdateTotalCount.Should().Be(updateTotalCount);
        previousPageRequest.PageSize.Size.Should().Be(pageSize);

        var previousPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, previousPageRequest);
        previousPageResult.PageNumber.Should().Be(totalPageCount - 1);
        previousPageResult.FirstId.Should().Be(previousPageResult.Items[0].EntityId);
        previousPageResult.LastId.Should().Be(previousPageResult.Items[^1].EntityId);

        previousPageResult.HasNext.Should().BeTrue();
        previousPageResult.HasPrevious.Should().BeTrue();

        previousPageResult.Items.Count.Should().Be(pageSize);
        previousPageResult.TotalCountSpecified.Should().Be(updateTotalCount);
        previousPageResult.TotalPages.Should().Be(updateTotalCount ? totalPageCount : -1);
        previousPageResult.TotalCount.Should().Be(updateTotalCount ? totalCount : -1);

        var backwardIndexChunk = pageSortDirection == PageSortDirection.AscendingByCreatedAt
            ? backwardIndexChunks[0]
            : backwardIndexChunks[0].OrderDescending().ToArray();
        var backwardResultIndexes = previousPageResult.Items.Select(tag => tag.Model.Other).ToArray();
        backwardResultIndexes.Should().BeEquivalentTo(backwardIndexChunk);

        for (var i = 1; i < remainingPages; i++)
        {
            //following requests are created from result since request chain is started
            previousPageRequest = previousPageResult.GetPreviousPage(previousPageRequest);
            previousPageRequest.PageCursor.IsFirstPage.Should().BeFalse(); //cursor will always point previous page

            previousPageRequest.PageCursor.IsFirstRequest.Should().BeFalse();
            previousPageRequest.PageCursor.FirstId.Should().Be(previousPageResult.FirstId);
            previousPageRequest.PageCursor.LastId.Should().Be(previousPageResult.LastId);

            previousPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, previousPageRequest);
            previousPageResult.HasNext.Should().BeTrue();

            var expectedPageNumber = totalPageCount - i - 1;
            var nextItems = (expectedPageNumber - 1) * pageSize;
            var expectedIndexes = itemIndexes.Skip((int)nextItems).Take(pageSize).ToArray();
            var expectedCount = expectedIndexes.Length;

            previousPageResult.PageSize.Should().Be(pageSize);
            previousPageResult.PageNumber.Should().Be(expectedPageNumber);
            previousPageResult.Items.Count.Should().BePositive();
            previousPageResult.Items.Count.Should().Be(expectedCount);
            previousPageResult.FirstId.Should().Be(previousPageResult.Items[0].EntityId);
            previousPageResult.LastId.Should().Be(previousPageResult.Items[^1].EntityId);
            previousPageResult.TotalCountSpecified.Should().BeFalse();
            previousPageResult.TotalCount.Should().Be(-1);
            previousPageResult.TotalPages.Should().Be(-1);

            var isFirstPage = previousPageResult.PageNumber == 1;
            if (isFirstPage)
            {
                previousPageResult.HasNext.Should().BeTrue();
                previousPageResult.HasPrevious.Should().BeFalse();
                previousPageResult.GetTotalCountUpToCurrentPage().Should().Be(pageSize);
            }
            else
            {
                previousPageResult.HasNext.Should().BeTrue();
                previousPageResult.HasPrevious.Should().BeTrue();
                previousPageResult.GetTotalCountUpToCurrentPage().Should().Be(previousPageRequest.PageNumber * pageSize);
            }

            backwardIndexChunk = pageSortDirection == PageSortDirection.AscendingByCreatedAt
                ? backwardIndexChunks[i]
                : backwardIndexChunks[i].OrderDescending().ToArray();
            backwardResultIndexes = previousPageResult.Items.Select(tag => tag.Model.Other).ToArray();
            backwardResultIndexes.Should().BeEquivalentTo(backwardIndexChunk);
        }
    }
}