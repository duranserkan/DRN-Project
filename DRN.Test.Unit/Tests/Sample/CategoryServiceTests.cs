using DRN.Framework.SharedKernel.Domain;
using Sample.Application.Services.QA.Categories;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Category;
using Category = Sample.Domain.QA.Category.CategoryEntity;

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

    [Fact]
    public async Task CreateResponseAsync_And_CreateResponsesAsync_Should_Map_Category_Responses()
    {
        var repository = Substitute.For<ICategoryRepository>();
        var service = new CategoryService(repository);
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
        var category = new Category("Mapped category")
        {
            EntityIdSource = new SourceKnownEntityId(new SourceKnownId(11, createdAt, 0, 0, 0), id, 3, true, false),
            ModifiedAt = createdAt.AddMinutes(3)
        };

        var response = await service.CreateResponseAsync(category);
        response.Id.Should().Be(id);
        response.CreatedAt.Should().Be(createdAt);
        response.ModifiedAt.Should().Be(createdAt.AddMinutes(3));
        response.Name.Should().Be("Mapped category");

        var batch = await service.CreateResponsesAsync([category]);
        batch.Should().ContainSingle().Which.Should().BeEquivalentTo(response);

        var created = await service.CreateEntityAsync(new CategoryPostRequest { Name = "Created" });
        created.Name.Should().Be("Created");

        Func<Task> nullEntity = () => service.CreateResponseAsync(null!);
        Func<Task> nullRequest = () => service.CreateEntityAsync(null!);
        await nullEntity.Should().ThrowAsync<ArgumentNullException>();
        await nullRequest.Should().ThrowAsync<ArgumentNullException>();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> canceledResponse = () => service.CreateResponseAsync(category, cancellation.Token);
        Func<Task> canceledCreate = () => service.CreateEntityAsync(new CategoryPostRequest { Name = "Canceled" }, cancellation.Token);
        await canceledResponse.Should().ThrowAsync<OperationCanceledException>();
        await canceledCreate.Should().ThrowAsync<OperationCanceledException>();
    }
}
