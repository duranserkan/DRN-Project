using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Entity;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.EntityFramework;

public class PaginationUtilsTests
{
    [Theory]
    [DataInline(100, 5, true, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(65, 20, false, PageSortDirection.AscendingByCreatedAt)]
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
            ? itemIndexes.Chunk(pageSize).Skip(1).ToArray()
            : itemIndexes.OrderDescending().Chunk(pageSize).Skip(1).ToArray();

        //Paginate Forward
        for (var i = 0; i < remainingPages; i++)
        {
            //following requests are created from result since request chain is started
            nextPageRequest = nextPageResult.GetNextPage(nextPageRequest);
            if (i == 0) //When next request is made for page 2 then cursor shows the previous page which is the first page
                nextPageRequest.PageCursor.IsFirstPage.Should().BeTrue();
            else
                nextPageRequest.PageCursor.IsFirstPage.Should().BeFalse();

            nextPageRequest.PageCursor.IsFirstRequest.Should().BeFalse();
            nextPageRequest.PageCursor.FirstId.Should().Be(nextPageResult.FirstId);
            nextPageRequest.PageCursor.LastId.Should().Be(nextPageResult.LastId);

            nextPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, nextPageRequest);
            nextPageResult.HasPrevious.Should().BeTrue();

            var expectedPageNumber = i + 2;
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

            var indexChunk = forwardIndexChunks[i];
            var resultIndexes = nextPageResult.Items.Select(tag => tag.Model.Other).ToArray();
            resultIndexes.Should().BeEquivalentTo(indexChunk);
        }

        //Paginate Backward
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

        var backwardIndexChunks = pageSortDirection == PageSortDirection.AscendingByCreatedAt
            ? itemIndexes.OrderDescending().Chunk(pageSize).Skip(1).ToArray()
            : itemIndexes.Order().Chunk(pageSize).Skip(1).ToArray();
        for (var i = 0; i < remainingPages; i++)
        {
            //following requests are created from result since request chain is started
            previousPageRequest = previousPageResult.GetPreviousPage(previousPageRequest);

            if (i == remainingPages - 1)
                previousPageRequest.PageCursor.IsFirstPage.Should().BeTrue();
            else
                previousPageRequest.PageCursor.IsFirstPage.Should().BeFalse();

            previousPageRequest.PageCursor.IsFirstRequest.Should().BeFalse();
            previousPageRequest.PageCursor.FirstId.Should().Be(previousPageResult.FirstId);
            previousPageRequest.PageCursor.LastId.Should().Be(previousPageResult.LastId);

            previousPageResult = await paginationUtils.ToPaginationResultAsync(tagQuery, previousPageRequest);
            previousPageResult.HasNext.Should().BeTrue();

            var expectedPageNumber = totalPageCount - 2 - i;
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

            var indexChunk = backwardIndexChunks[i];
            var resultIndexes = previousPageResult.Items.Select(tag => tag.Model.Other).ToArray();
            resultIndexes.Should().BeEquivalentTo(indexChunk);
        }
    }

    //todo add tests to go same page
    //todo add tests to go previous page
    //todo add tests to jump forward page
    //todo add tests to jump previous page

    /*[Theory]
    [DataInline(100, 5, true, PageSortDirection.DescendingByCreatedAt)]
    [DataInline(65, 20, true, PageSortDirection.AscendingByCreatedAt)]
    public async Task PaginationUtils_Should_Jump_Forward(TestContext context, int totalCount, int pageSize, bool updateTotalCount, PageSortDirection pageSortDirection)
    {
        _ = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
        var qaContext = context.GetRequiredService<QAContext>();
        var paginationUtils = context.GetRequiredService<IPaginationUtils>();

        var totalPageCount = (long)Math.Ceiling((decimal)totalCount / pageSize);
        var tagPrefix = $"{nameof(PaginationUtils_Should_Jump_Forward)}_{Guid.NewGuid():N}";


        //Initial Page Result
        var itemIndexes = Enumerable.Range(0, totalCount).ToArray();
        var tags = itemIndexes.Select(index => new Tag($"{tagPrefix}_{index}") { Model = new TagValueModel { Other = index } }).ToArray();
        await qaContext.Tags.AddRangeAsync(tags);
        await qaContext.SaveChangesAsync();

        var tagQuery = qaContext.Tags.Where(t => t.Name.StartsWith(tagPrefix));
        var request = PaginationRequest.DefaultWith(pageSize, updateTotalCount, pageSortDirection);
        var paginationResult = await paginationUtils.ToPaginationResultAsync(tagQuery, request);

        request.PageCursor.LastId.Should().Be(Guid.Empty);
        request.PageCursor.PageNumber.Should().Be(1);
        request.PageCursor.IsFirstPage.Should().BeTrue();
        request.PageCursor.IsFirstRequest.Should().BeTrue();
        request.UpdateTotalCount.Should().Be(updateTotalCount);
        request.PageSize.Size.Should().Be(pageSize);

        paginationResult.LastId.Should().Be(paginationResult.Items[^1].EntityId);

        paginationResult.HasNext.Should().BeTrue();
        paginationResult.HasPrevious.Should().BeFalse();

        paginationResult.Items.Count.Should().Be(pageSize);
        paginationResult.TotalCountSpecified.Should().Be(updateTotalCount);
        paginationResult.TotalPages.Should().Be(updateTotalCount ? totalPageCount : -1);
        paginationResult.TotalCount.Should().Be(updateTotalCount ? totalCount : -1);

        var nextRequest = request;
        var nextResult = paginationResult;

        var indexChunks = pageSortDirection == PageSortDirection.AscendingByCreatedAt
            ? itemIndexes.Chunk(pageSize).Skip(1).ToArray()
            : itemIndexes.OrderDescending().Chunk(pageSize).Skip(1).ToArray();

        nextRequest = nextResult.GetPageJump(nextRequest, 3);
        nextRequest.PageCursor.IsFirstPage.Should().BeTrue();
        nextRequest.PageCursor.IsFirstRequest.Should().BeFalse();
        nextRequest.PageCursor.LastId.Should().NotBe(Guid.Empty);

        nextResult = await paginationUtils.ToPaginationResultAsync(tagQuery, nextRequest);
        nextResult.HasPrevious.Should().BeTrue();

        var expectedPageNumber = 3;
        var previousItems = (expectedPageNumber - 1) * pageSize;
        var expectedIndexes = itemIndexes.Skip(previousItems).Take(pageSize).ToArray();
        var expectedCount = expectedIndexes.Length;

        nextResult.PageSize.Should().Be(pageSize);
        nextResult.PageNumber.Should().Be(expectedPageNumber);
        nextResult.Items.Count.Should().BePositive();
        nextResult.Items.Count.Should().Be(expectedCount);
        nextResult.LastId.Should().Be(nextResult.Items[^1].EntityId);
        nextResult.TotalCountSpecified.Should().BeFalse();
        nextResult.TotalCount.Should().Be(-1);
        nextResult.TotalPages.Should().Be(-1);

        var isLastPage = nextResult.PageNumber == totalPageCount;
        if (isLastPage)
        {
            nextResult.HasNext.Should().BeFalse();
            nextResult.GetTotalCountUpToCurrentPage().Should().Be(totalCount);
        }
        else
        {
            nextResult.HasNext.Should().BeTrue();
            nextResult.GetTotalCountUpToCurrentPage().Should().Be(nextRequest.PageNumber * pageSize);
        }

        var thirdPageIndexes = indexChunks[2];
        var resultIndexes = nextResult.Items.Select(tag => tag.Model.Other);
        resultIndexes.Should().BeEquivalentTo(thirdPageIndexes);
    }*/
}