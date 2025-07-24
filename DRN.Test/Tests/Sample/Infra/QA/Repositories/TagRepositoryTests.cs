using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using Sample.Domain.QA.Tags;
using Sample.Infra;

namespace DRN.Test.Tests.Sample.Infra.QA.Repositories;

public class TagRepositoryTests
{
    [Theory]
    [DataInline]
    public async Task TagRepository_Should_Implement_SourceKnownRepository_Functionalities(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.Isolated.ApplyMigrationsAsync();
        var repository = context.GetRequiredService<ITagRepository>();

        var tagPrefix = $"{nameof(TagRepository_Should_Implement_SourceKnownRepository_Functionalities)}_{Guid.NewGuid():N}";
        var (firstTag, secondTag, thirdTag) = TagGenerator.GetTags(tagPrefix);

        var beforeTagCreation = DateTimeOffset.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(1.2));

        repository.Add(firstTag);
        repository.Add(secondTag);
        repository.Add(thirdTag);

        await repository.SaveChangesAsync();

        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var afterTagCreation = DateTimeOffset.UtcNow;

        AssertValidations(firstTag, repository);
        await AssertCRUD(repository, firstTag, secondTag, thirdTag, tagPrefix);
        await AssertPagination(beforeTagCreation, afterTagCreation, repository, firstTag, secondTag);
    }

    private static async Task AssertCRUD(ITagRepository repository, Tag firstTag, Tag secondTag, Tag thirdTag, string tagPrefix)
    {
        var tagFromDb = await repository.GetAsync(firstTag.EntityId);
        tagFromDb.Should().Be(firstTag);
        tagFromDb.Name.Should().Be(firstTag.Name);
        tagFromDb.Model.Should().BeEquivalentTo(firstTag.Model);

        var tagFromDb2 = await repository.GetAsync(secondTag.EntityId);
        tagFromDb2.Should().Be(secondTag);
        tagFromDb2.Name.Should().Be(secondTag.Name);
        tagFromDb2.Model.Should().BeEquivalentTo(secondTag.Model);

        var tagFromDb3 = await repository.GetAsync(thirdTag.EntityId);
        var deletedCount = await repository.DeleteAsync(tagFromDb3);
        deletedCount.Should().Be(1);

        var getDeleted = async () => await repository.GetAsync(thirdTag.EntityId);
        await getDeleted.Should().ThrowExactlyAsync<NotFoundException>();

        repository.Add(thirdTag);
        await repository.SaveChangesAsync();
        tagFromDb3 = await repository.GetOrDefaultAsync(thirdTag.EntityId);
        tagFromDb3.Should().Be(thirdTag);

        deletedCount = await repository.DeleteAsync(tagFromDb3.EntityId);
        deletedCount.Should().Be(1);

        var tagsFromDb = await repository.GetAsync([tagFromDb3.EntityId]);
        tagsFromDb.Should().BeEmpty();

        var fourthTag = TagGenerator.New(tagPrefix, "fourthTag");
        var changeCount = await repository.CreateAsync(fourthTag);
        changeCount.Should().Be(1);

        repository.Remove(fourthTag);
        changeCount = await repository.SaveChangesAsync();
        changeCount.Should().Be(1);

        tagFromDb3 = await repository.GetOrDefaultAsync(fourthTag.EntityId);
        tagFromDb3.Should().BeNull();
    }

    private static void AssertValidations(Tag firstTag, ITagRepository repository)
    {
        var validEntityId = firstTag.EntityId;
        var invalidEntityId = Guid.NewGuid();

        repository.ValidateEntityId(validEntityId).Valid.Should().BeTrue();
        repository.ValidateEntityId(invalidEntityId, false).Valid.Should().BeFalse();
        var validationAction = () => repository.ValidateEntityId(invalidEntityId);
        validationAction.Should().Throw<ValidationException>();

        var ids = repository.ValidateEntityIds([validEntityId, invalidEntityId], false);
        ids[0].Valid.Should().BeTrue();
        ids[1].Valid.Should().BeFalse();

        var validationAction2 = () => repository.ValidateEntityIds([validEntityId, invalidEntityId]);
        validationAction2.Should().Throw<ValidationException>();

        var idsEnumerable = repository.ValidateEntityIdsAsEnumerable([validEntityId, invalidEntityId], false);
        ids = idsEnumerable.ToArray();
        ids[0].Valid.Should().BeTrue();
        ids[1].Valid.Should().BeFalse();

        idsEnumerable = repository.ValidateEntityIdsAsEnumerable([validEntityId, invalidEntityId]);
        validationAction2 = () => idsEnumerable.ToArray();
        validationAction2.Should().Throw<ValidationException>();
    }
    
    private static async Task AssertPagination(DateTimeOffset beforeTagCreation, DateTimeOffset afterTagCreation, ITagRepository repository, Tag firstTag, Tag secondTag)
    {
        var selectAll = EntityCreatedFilter.Between(beforeTagCreation, afterTagCreation);
        var paginationResult = await repository.PaginateAsync(PaginationRequest.Default, selectAll);
        paginationResult.Items[0].Should().Be(firstTag);
        paginationResult.Items[1].Should().Be(secondTag);

        var selectNone = EntityCreatedFilter.Outside(beforeTagCreation, afterTagCreation);
        paginationResult = await repository.PaginateAsync(PaginationRequest.Default, selectNone);
        paginationResult.ItemCount.Should().Be(0);

        paginationResult = await repository.PaginateAsync(PaginationRequest.Default);
        paginationResult.Items[0].Should().Be(firstTag);
        paginationResult.Items[1].Should().Be(secondTag);

        var index = 0;
        var paginateSingle = PaginationRequest.DefaultWith(1);
        await foreach (var paginationResult2 in repository.PaginateAllAsync(paginateSingle))
        {
            paginationResult2.Items[0].Should().Be(paginationResult.Items[index]);
            index++;
        }
        
        index = 0;
        await foreach (var paginationResult3 in repository.PaginateAllAsync(paginateSingle, selectNone))
        {
            paginationResult3.ItemCount.Should().Be(0);
            index++;
        }

        index.Should().Be(1);
    }
}