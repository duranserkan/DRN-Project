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
}