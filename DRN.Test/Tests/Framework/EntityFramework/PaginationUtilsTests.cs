using DRN.Framework.EntityFramework;
using DRN.Framework.SharedKernel.Domain;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Hosted;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.EntityFramework;

public class PaginationUtilsTests
{
    //todo tests should include PageSortDirection.DescendingByCreatedAt
    //todo add empty pagination tests
    //todo verify index order and indexes
    //todo update total count on last pagination request
    //todo normalize counts to support long 
    //todo add missing validations
    [Theory]
    [DataInline(100, 5, true)]
    [DataInline(65, 20, false)]
    public async Task PaginationUtils_Should_Return_Paginated_Result(TestContext context, int totalCount, int pageSize, bool updateTotalCount)
    {
        _ = await context.ApplicationContext.CreateApplicationAndBindDependenciesAsync<SampleProgram>();
        var qaContext = context.GetRequiredService<QAContext>();
        var paginationUtils = context.GetRequiredService<IPaginationUtils>();

        var totalPageCount = (long)Math.Ceiling((decimal)totalCount / pageSize);
        var tagPrefix = $"{nameof(PaginationUtils_Should_Return_Paginated_Result)}_{Guid.NewGuid():N}";

        var itemIndexes = Enumerable.Range(0, totalCount).ToArray();
        var tags = itemIndexes.Select(index => new Tag($"{tagPrefix}_{index}") { Model = new TagValueModel { Other = index } }).ToArray();
        await qaContext.Tags.AddRangeAsync(tags);
        await qaContext.SaveChangesAsync();

        var tagQuery = qaContext.Tags.Where(t => t.Name.StartsWith(tagPrefix));
        var request = PaginationRequest.DefaultWith(pageSize, updateTotalCount);
        var paginationResult = await paginationUtils.ToPaginationResultAsync(tagQuery, request);

        request.PageCursor.LastId.Should().Be(Guid.Empty);
        request.PageCursor.PageNumber.Should().Be(1);
        request.PageCursor.IsFirstPage.Should().BeTrue();
        request.PageCursor.IsFirstPage.Should().BeTrue();

        paginationResult.LastId.Should().Be(paginationResult.Items.Last().EntityId);
        
        paginationResult.HasNext.Should().BeTrue();
        paginationResult.HasPrevious.Should().BeFalse();

        paginationResult.Items.Count.Should().Be(pageSize);
        paginationResult.TotalCountSpecified.Should().Be(updateTotalCount);
        paginationResult.TotalPages.Should().Be(updateTotalCount ? totalPageCount : -1);
        paginationResult.TotalCount.Should().Be(updateTotalCount ? totalCount : -1);
        
        var remainingPages = totalPageCount - 1;
        var nextRequest = request;
        var nextResult = paginationResult;
        for (var i = 0; i < remainingPages; i++)
        {
            var nextTagQuery = qaContext.Tags.Where(t => t.Name.StartsWith(tagPrefix));

            nextRequest = nextRequest.GetNextPage(nextResult.LastId);
            nextRequest.PageCursor.IsFirstPage.Should().BeFalse();
            nextRequest.PageCursor.IsFirstPage.Should().BeFalse();
            nextRequest.PageCursor.LastId.Should().NotBe(Guid.Empty);

            
            nextResult = await paginationUtils.ToPaginationResultAsync(nextTagQuery, nextRequest);
            nextResult.HasPrevious.Should().BeTrue();

            var expectedPageNumber = i + 2;
            var previousItems = (expectedPageNumber - 1) * pageSize;
            var expectedIndexes = itemIndexes.Skip(previousItems).Take(pageSize).ToArray();
            var expectedCount = expectedIndexes.Length;

            nextResult.PageNumber.Should().Be(expectedPageNumber);
            nextResult.Items.Count.Should().BePositive();
            nextResult.Items.Count.Should().Be(expectedCount);
            nextResult.LastId.Should().Be(nextResult.Items.Last().EntityId);

            var isLastPage = nextResult.PageNumber == totalPageCount;
            if (isLastPage)
                nextResult.HasNext.Should().BeFalse();
            else
                nextResult.HasNext.Should().BeTrue();
        }
    }
}