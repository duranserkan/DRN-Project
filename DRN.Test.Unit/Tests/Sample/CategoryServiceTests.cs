using DRN.Framework.SharedKernel.Domain;
using Sample.Application.Services;
using Sample.Domain.QA.Categories;

namespace DRN.Test.Unit.Tests.Sample;

public class CategoryServiceTests
{
    [Fact]
    public async Task Lookup_Should_Delegate_To_Repository_And_Preserve_Missing_Results()
    {
        var repository = Substitute.For<ICategoryRepository>();
        var service = new CategoryService(repository);
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        var category = new Category("Test category");
        repository.GetOrDefaultAsync(id, false, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(category);

        (await service.GetOrDefaultAsync(id, cancellation.Token)).Should().BeSameAs(category);
        repository.Received(1).CancelWhen(cancellation.Token);
        await repository.Received(1).GetOrDefaultAsync(id, false, SourceKnownEntityIdFormat.ConfiguredDefault);

        repository.GetOrDefaultAsync(id, false, SourceKnownEntityIdFormat.ConfiguredDefault).Returns((Category?)null);
        (await service.GetOrDefaultAsync(id)).Should().BeNull();

        repository.ClearReceivedCalls();
        cancellation.Cancel();
        Func<Task> lookup = () => service.GetOrDefaultAsync(id, cancellation.Token);
        await lookup.Should().ThrowAsync<OperationCanceledException>();
        repository.ReceivedCalls().Should().BeEmpty();
    }
}
