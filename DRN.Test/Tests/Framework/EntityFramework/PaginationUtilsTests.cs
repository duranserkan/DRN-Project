using DRN.Framework.EntityFramework;
using DRN.Framework.SharedKernel.Domain;
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
        paginationResult = await paginationUtils.ToPaginationResultAsync(tagQuery, request);

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

        
        //Remaining Pages
        var remainingPages = totalPageCount - 1;
        var nextRequest = request;
        var nextResult = paginationResult;

        var indexChunks = pageSortDirection == PageSortDirection.AscendingByCreatedAt
            ? itemIndexes.Chunk(pageSize).Skip(1).ToArray()
            : itemIndexes.OrderDescending().Chunk(pageSize).Skip(1).ToArray();

        for (var i = 0; i < remainingPages; i++)
        {
            nextRequest = nextResult.GetNextPage(nextRequest);
            nextRequest.PageCursor.IsFirstPage.Should().BeFalse();
            nextRequest.PageCursor.IsFirstRequest.Should().BeFalse();
            nextRequest.PageCursor.LastId.Should().NotBe(Guid.Empty);

            nextResult = await paginationUtils.ToPaginationResultAsync(tagQuery, nextRequest);
            nextResult.HasPrevious.Should().BeTrue();

            var expectedPageNumber = i + 2;
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

            var indexChunk = indexChunks[i];
            var resultIndexes = nextResult.Items.Select(tag => tag.Model.Other);
            resultIndexes.Should().BeEquivalentTo(indexChunk);
        }
    }
}