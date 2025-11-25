using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Test.Integration.Tests.Sample.Infra.QA.Repositories.Data;
using Sample.Domain.QA.Tags;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Sample.Infra.QA.Repositories;

public class TagRepositoryTests
{
    [Theory]
    [DataInline]
    public async Task TagRepository_Should_Implement_SourceKnownRepository_Functionalities(DrnTestContext context)
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

        var prefixFilter = "ShouldContainPrefix";
        repository.Settings.AddFilter(prefixFilter, tag => tag.Name.Contains(tagPrefix));
        repository.Settings.Filters.ContainsKey(prefixFilter).Should().BeTrue();
        repository.Settings.Filters.Count.Should().Be(1);
        var tags = await repository.GetAllAsync();
        tags.Length.Should().Be(3);

        var maxValueFilter = "GreaterThanMax-2";
        repository.Settings.AddFilter(maxValueFilter, tag => tag.Model.Other > long.MaxValue - 2);
        repository.Settings.Filters.ContainsKey(maxValueFilter).Should().BeTrue();
        repository.Settings.Filters.Count.Should().Be(2);
        tags = await repository.GetAllAsync();
        tags.Length.Should().Be(1);
        
        repository.Settings.ClearFilters();
        repository.Settings.Filters.Count.Should().Be(0);

        maxValueFilter = "GreaterThanMax-3";
        repository.Settings.AddFilter(maxValueFilter, tag => tag.Model.Other > long.MaxValue - 3);
        repository.Settings.Filters.ContainsKey(maxValueFilter).Should().BeTrue();
        repository.Settings.Filters.Count.Should().Be(1);
        tags = await repository.GetAllAsync();
        tags.Length.Should().Be(2);

        repository.Settings.ClearFilters();
        repository.Settings.Filters.Count.Should().Be(0);
        
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var afterTagCreation = DateTimeOffset.UtcNow;

        AssertValidations(firstTag, repository);
        await AssertCrud(repository, firstTag, secondTag, thirdTag, tagPrefix);
        await AssertPagination(beforeTagCreation, afterTagCreation, repository, firstTag, secondTag);

        var firstPageResult = await repository.PaginateAsync(pageSize: 1, direction: PageSortDirection.Descending, updateTotalCount: true);
        firstPageResult.Info.Request.PageCursor.IsFirstRequest.Should().BeTrue();
        firstPageResult.Items.Count.Should().Be(1);
        firstPageResult.Info.Total.Count.Should().BeGreaterThan(1);
        firstPageResult.Info.Request.PageCursor.SortDirection.Should().Be(PageSortDirection.Descending);

        var secondPageResult = await repository.PaginateAsync(firstPageResult.Info, 2, pageSize: 1);
        secondPageResult.Info.Request.PageNumber.Should().Be(2);
        secondPageResult.Items.Count.Should().Be(1);
        secondPageResult.Info.Total.Count.Should().BeGreaterThan(1);
        secondPageResult.Info.Request.PageCursor.SortDirection.Should().Be(PageSortDirection.Descending);

        var resetDirectionResult = await repository.PaginateAsync(firstPageResult.Info, 2, pageSize: 1, direction: PageSortDirection.Ascending,
            totalCount: secondPageResult.Info.Total.Count);
        resetDirectionResult.Info.Request.PageCursor.IsFirstRequest.Should().BeTrue();
        resetDirectionResult.Items.Count.Should().Be(1);
        resetDirectionResult.Info.Total.Count.Should().BeGreaterThan(1);
        resetDirectionResult.Info.Request.PageCursor.SortDirection.Should().Be(PageSortDirection.Ascending);
        resetDirectionResult.Info.Total.Count.Should().Be(secondPageResult.Info.Total.Count);

        var resetSizeResult = await repository.PaginateAsync(firstPageResult.Info, 2, pageSize: 2, direction: PageSortDirection.Descending,
            totalCount: secondPageResult.Info.Total.Count);
        resetSizeResult.Info.Request.PageCursor.IsFirstRequest.Should().BeTrue();
        resetSizeResult.Items.Count.Should().Be(2);
        resetSizeResult.Info.Total.Count.Should().BeGreaterThan(1);
        resetSizeResult.Info.Request.PageCursor.SortDirection.Should().Be(PageSortDirection.Descending);
        resetSizeResult.Info.Total.Count.Should().Be(secondPageResult.Info.Total.Count);

        await AssertRepositorySettings(context, tagPrefix);
    }


    private static async Task AssertCrud(ITagRepository repository, Tag firstTag, Tag secondTag, Tag thirdTag, string tagPrefix)
    {
        var tagFromDb = await repository.GetAsync(firstTag.EntityId);
        tagFromDb.Should().Be(firstTag);
        tagFromDb.Name.Should().Be(firstTag.Name);
        tagFromDb.Model.Should().BeEquivalentTo(firstTag.Model);

        var tagFromDb2 = await repository.GetAsync(secondTag.EntityIdSource);
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

        var tagFromDb4 = await repository.GetAsync([fourthTag.EntityIdSource]);
        tagFromDb4.Should().BeEquivalentTo([fourthTag]);

        repository.Remove(fourthTag);
        changeCount = await repository.SaveChangesAsync();
        changeCount.Should().Be(1);

        var fourthTagDeleted = await repository.GetOrDefaultAsync(fourthTag.EntityIdSource);
        fourthTagDeleted.Should().BeNull();

        tagFromDb3 = await repository.GetOrDefaultAsync(fourthTag.EntityId);
        tagFromDb3.Should().BeNull();

        var fifthTag = TagGenerator.New(tagPrefix, "fifthTag");
        await repository.CreateAsync(fifthTag);
        var fifthTagDeleted = await repository.DeleteAsync(fifthTag.EntityIdSource);
        fifthTagDeleted.Should().Be(1);

        var stringValue = Guid.NewGuid().ToString("N");
        var sixthTag = TagGenerator.New(tagPrefix, "sixthTag");
        sixthTag.Model.StringValue = stringValue;
        sixthTag.Model.BoolValue = true;

        var seventhTag = TagGenerator.New(tagPrefix, "seventhTag");
        seventhTag.Model.StringValue = stringValue;
        seventhTag.Model.BoolValue = false;

        var any = await repository.AnyAsync(t => t.Model.StringValue == stringValue && t.Model.BoolValue);
        any.Should().BeFalse();

        var createdCount = await repository.CreateAsync(sixthTag, seventhTag);
        createdCount.Should().Be(2);

        var all = await repository.AllAsync(t => t.Id < 0);
        all.Should().BeTrue();

        all = await repository.AllAsync(t => t.Id == 0);
        all.Should().BeFalse();

        any = await repository.AnyAsync(t => t.Model.StringValue == stringValue && t.Model.BoolValue);
        any.Should().BeTrue();

        var count = await repository.CountAsync(t => t.Model.StringValue == stringValue);
        count.Should().Be(2);

        count = await repository.CountAsync(t => t.Model.StringValue == stringValue && t.Model.BoolValue);
        count.Should().Be(1);

        count = await repository.CountAsync(t => t.Model.StringValue == stringValue && !t.Model.BoolValue);
        count.Should().Be(1);

        await repository.DeleteAsync(sixthTag, seventhTag);
        count = await repository.CountAsync(t => t.Model.StringValue == stringValue);
        count.Should().Be(0);
    }

    private static void AssertValidations(Tag firstTag, ITagRepository repository)
    {
        var validEntityId = firstTag.EntityId;
        var invalidEntityId = Guid.NewGuid();

        repository.GetEntityId(validEntityId).Valid.Should().BeTrue();
        repository.GetEntityId(invalidEntityId, false).Valid.Should().BeFalse();
        var validationAction = () => repository.GetEntityId(invalidEntityId);
        validationAction.Should().Throw<ValidationException>();

        validationAction = () => repository.GetEntityId<Tag>(invalidEntityId);
        validationAction.Should().Throw<ValidationException>();

        var ids = repository.GetEntityIds([validEntityId, invalidEntityId], false);
        ids[0].Valid.Should().BeTrue();
        ids[1].Valid.Should().BeFalse();

        var validationAction2 = () => repository.GetEntityIds([validEntityId, invalidEntityId]);
        validationAction2.Should().Throw<ValidationException>();

        validationAction2 = () => repository.GetEntityIds<Tag>([validEntityId, invalidEntityId]);
        validationAction2.Should().Throw<ValidationException>();

        var idsEnumerable = repository.GetEntityIdsAsEnumerable([validEntityId, invalidEntityId], false);
        ids = idsEnumerable.ToArray();
        ids[0].Valid.Should().BeTrue();
        ids[1].Valid.Should().BeFalse();

        idsEnumerable = repository.GetEntityIdsAsEnumerable([validEntityId, invalidEntityId]);
        var enumerable = idsEnumerable;

        validationAction2 = () => enumerable.ToArray();
        validationAction2.Should().Throw<ValidationException>();

        idsEnumerable = repository.GetEntityIdsAsEnumerable<Tag>([validEntityId, invalidEntityId]);
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
        paginationResult.Info.ItemCount.Should().Be(0);

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
            paginationResult3.Info.ItemCount.Should().Be(0);
            index++;
        }

        index.Should().Be(1);
    }

    private static async Task AssertRepositorySettings(DrnTestContext context, string tagPrefix)
    {
        var scope1 = context.CreateScope();
        var repository1 = scope1.ServiceProvider.GetRequiredService<ITagRepository>();

        var settingsTag = TagGenerator.New(tagPrefix, "settingsTag");
        var settingsQuestion = QuestionGenerator.New(tagPrefix, "settingsTagQuestion");
        settingsTag.Questions.Add(settingsQuestion);

        await repository1.CreateAsync(settingsTag);

        var scope2 = context.CreateScope();
        var repository2 = scope2.ServiceProvider.GetRequiredService<ITagRepository>();
        var qaContext2 = scope2.ServiceProvider.GetRequiredService<QAContext>();

        var tagFromDb2 = await repository2.GetAsync(settingsTag.EntityIdSource);
        var questionsFromDb2 = tagFromDb2.Questions;
        questionsFromDb2.Count.Should().Be(1);
        var entry2 = qaContext2.ChangeTracker.Entries<Tag>().ToArray();
        entry2.Length.Should().Be(1);

        var scope3 = context.CreateScope();
        var repository3 = scope3.ServiceProvider.GetRequiredService<ITagRepository>();
        var qaContext3 = scope3.ServiceProvider.GetRequiredService<QAContext>();
        repository3.Settings.IgnoreAutoIncludes = true;
        repository3.Settings.AsNoTracking = true;

        var tagFromDb3 = await repository3.GetAsync(settingsTag.EntityIdSource);
        var questionsFromDb3 = tagFromDb3.Questions;
        questionsFromDb3.Count.Should().Be(0);

        var entry3 = qaContext3.ChangeTracker.Entries<Tag>().ToArray();
        entry3.Length.Should().Be(0);
    }
}