using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.SharedKernel.Cancellation;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Cancellation;
using DRN.Framework.Utils.Entity;
using DRN.Test.Integration.Tests.Sample.Infra.QA.Repositories.Data;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Tags;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Integration.Tests.Sample.Infra.QA.Repositories;

public class TagRepositoryTests
{
    private const long PaginationFilterMinimum = 100;

    [Theory]
    [DataInline]
    public async Task TagRepository_Should_Implement_SourceKnownRepository_Functionalities(DrnTestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.Isolated.ApplyMigrationsAsync();
        var repository = context.GetRequiredService<ITagRepository>();
        AssertSqlQueryTags(context);

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
        await AssertCrud(context, repository, firstTag, secondTag, thirdTag, tagPrefix);
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
        await AssertCancellation(context);
    }


    private static async Task AssertCrud(DrnTestContext context, ITagRepository repository, Tag firstTag, Tag secondTag, Tag thirdTag, string tagPrefix)
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

        await AssertCrossEntityIdCollectionValidation(context, repository, tagPrefix);
    }

    private static async Task AssertCrossEntityIdCollectionValidation(DrnTestContext context, ITagRepository repository, string tagPrefix)
    {
        using var scope = context.CreateScope();
        var qaContext = scope.ServiceProvider.GetRequiredService<QAContext>();
        var category = new Category($"{tagPrefix}_wrong_type_category");
        qaContext.Categories.Add(category);
        await qaContext.SaveChangesAsync();

        var wrongTypeIds = new[] { category.EntityIdSource };

        var readAction = async () => await repository.GetAsync(wrongTypeIds);
        await readAction.Should().ThrowAsync<ValidationException>();

        var deleteAction = async () => await repository.DeleteAsync(wrongTypeIds);
        await deleteAction.Should().ThrowAsync<ValidationException>();

        var getOrDefaultWithWrongTypeAction = async () => await repository.GetOrDefaultAsync(category.EntityIdSource, validate: true);
        await getOrDefaultWithWrongTypeAction.Should().ThrowAsync<ValidationException>();

        var getOrDefaultWithWrongTypeGuidAction = async () => await repository.GetOrDefaultAsync(category.EntityId, validate: true);
        await getOrDefaultWithWrongTypeGuidAction.Should().ThrowAsync<ValidationException>();

        var getOrDefaultWithWrongTypeNoValidate = await repository.GetOrDefaultAsync(category.EntityIdSource, validate: false);
        getOrDefaultWithWrongTypeNoValidate.Should().BeNull();

        var getOrDefaultWithWrongTypeGuidNoValidate = await repository.GetOrDefaultAsync(category.EntityId, validate: false);
        getOrDefaultWithWrongTypeGuidNoValidate.Should().BeNull();
    }

    private static void AssertValidations(Tag firstTag, ITagRepository repository)
    {
        var validEntityId = firstTag.EntityId;
        var invalidEntityId = Guid.NewGuid();

        repository.GetEntityId(validEntityId).Valid.Should().BeTrue();
        repository.GetEntityId(invalidEntityId, false).Valid.Should().BeFalse();

        repository.GetEntityId(null).Should().BeNull();
        var validationAction = () => repository.GetEntityId(invalidEntityId);
        validationAction.Should().Throw<ValidationException>();

        repository.GetEntityId<Tag>(null).Should().BeNull();
        validationAction = () => repository.GetEntityId<Tag>(invalidEntityId);
        validationAction.Should().Throw<ValidationException>();

        var ids = repository.GetEntityIds([validEntityId, invalidEntityId], false);
        ids[0].Valid.Should().BeTrue();
        ids[1].Valid.Should().BeFalse();

        repository.GetEntityIds([null]).First().Should().BeNull();
        var validationAction2 = () => repository.GetEntityIds([validEntityId, invalidEntityId]);
        validationAction2.Should().Throw<ValidationException>();

        repository.GetEntityIds<Tag>([null]).First().Should().BeNull();
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

        index.Should().Be(paginationResult.Items.Count);
        index = 0;
        await foreach (var paginationResult3 in repository.PaginateAllAsync(paginateSingle, selectNone))
        {
            paginationResult3.Info.ItemCount.Should().Be(0);
            index++;
        }

        index.Should().Be(1);

        await AssertPaginationWithSettingsFilters(repository);
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

    private static void AssertSqlQueryTags(DrnTestContext context)
    {
        using var scope = context.CreateScope();
        var repository = new QueryTagRepository(
            scope.ServiceProvider.GetRequiredService<QAContext>(),
            scope.ServiceProvider.GetRequiredService<IEntityUtils>());

        var sql = repository.GetTaggedQueryString();

        sql.Should().Contain(typeof(QueryTagRepository).FullName!);
        sql.Should().Contain(nameof(QueryTagRepository.GetTaggedQueryString));
    }

    private sealed class QueryTagRepository : SourceKnownRepository<QAContext, Tag>
    {
        public QueryTagRepository(QAContext context, IEntityUtils utils)
            : base(context, utils)
        {
            Settings.ScopeKey = CancellationScopeKey.For<QueryTagRepository>();
        }

        public string GetTaggedQueryString() => EntitiesWithAppliedSettings().ToQueryString();
    }

    private sealed class AlternateQueryTagRepository : SourceKnownRepository<QAContext, Tag>
    {
        internal static readonly CancellationScopeKey ScopeKey = CancellationScopeKey.For<AlternateQueryTagRepository>("alternate");

        public AlternateQueryTagRepository(QAContext context, IEntityUtils utils)
            : base(context, utils)
        {
            Settings.ScopeKey = ScopeKey;
        }
    }

    private static async Task AssertCancellation(DrnTestContext context)
    {
        AssertNullScopeKeyDefaultRootCancellation(context);
        await AssertRepositoryTokenComposition(context, cancelFirstToken: true);
        await AssertRepositoryTokenComposition(context, cancelFirstToken: false);
        await AssertCancelChangesIsolation(context);
        await AssertRootCancellation(context);
        await AssertRepositoryScopeSharingAndKeyIsolation(context);
        AssertParentServiceScopeIsolation(context);
    }

    private static void AssertNullScopeKeyDefaultRootCancellation(DrnTestContext context)
    {
        using var scope = context.CreateScope();
        var cancellation = scope.ServiceProvider.GetRequiredService<ICancellationUtils>();
        var repository = scope.ServiceProvider.GetRequiredService<ITagRepository>();

        repository.Settings.ScopeKey.Should().BeNull();
        repository.CancellationToken.Should().Be(cancellation.Root.Token);

        repository.CancelChanges();
        cancellation.Root.IsCancellationRequested.Should().BeTrue();
    }

    private static async Task AssertRepositoryTokenComposition(DrnTestContext context, bool cancelFirstToken)
    {
        using var scope = context.CreateScope();
        using var firstSource = new CancellationTokenSource();
        using var secondSource = new CancellationTokenSource();
        var cancellation = scope.ServiceProvider.GetRequiredService<ICancellationUtils>();
        var repository = scope.ServiceProvider.GetRequiredService<ITagRepository>();
        repository.Settings.ScopeKey = CancellationScopeKey.For<ITagRepository>();
        var stableToken = repository.CancellationToken;

        cancellation.Root.Token.Should().NotBe(stableToken);

        repository.CancelWhen(firstSource.Token);
        repository.CancellationToken.Should().Be(stableToken);
        repository.CancelWhen(secondSource.Token);
        repository.CancellationToken.Should().Be(stableToken);

        if (cancelFirstToken)
            firstSource.Cancel();
        else
            secondSource.Cancel();

        stableToken.IsCancellationRequested.Should().BeTrue();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
        await AssertQueryCancellation(repository, stableToken);
    }

    private static async Task AssertCancelChangesIsolation(DrnTestContext context)
    {
        using var scope = context.CreateScope();
        var cancellation = scope.ServiceProvider.GetRequiredService<ICancellationUtils>();
        var repository = scope.ServiceProvider.GetRequiredService<ITagRepository>();
        repository.Settings.ScopeKey = CancellationScopeKey.For<ITagRepository>();
        var unrelatedScope = cancellation.GetOrCreateScope(CancellationScopeKey.For<TagRepositoryTests>("unrelated-repository"));
        var effectiveToken = repository.CancellationToken;

        repository.CancelChanges();

        effectiveToken.IsCancellationRequested.Should().BeTrue();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
        unrelatedScope.IsCancellationRequested.Should().BeFalse();
        await AssertQueryCancellation(repository, effectiveToken);
    }

    private static async Task AssertRootCancellation(DrnTestContext context)
    {
        using var scope = context.CreateScope();
        var cancellation = scope.ServiceProvider.GetRequiredService<ICancellationUtils>();
        var repository = scope.ServiceProvider.GetRequiredService<ITagRepository>();
        var effectiveToken = repository.CancellationToken;

        cancellation.Root.Cancel();

        cancellation.Root.IsCancellationRequested.Should().BeTrue();
        await AssertQueryCancellation(repository, effectiveToken);
    }

    private static async Task AssertRepositoryScopeSharingAndKeyIsolation(DrnTestContext context)
    {
        using var scope = context.CreateScope();
        var cancellation = scope.ServiceProvider.GetRequiredService<ICancellationUtils>();
        var qaContext = scope.ServiceProvider.GetRequiredService<QAContext>();
        var entityUtils = scope.ServiceProvider.GetRequiredService<IEntityUtils>();
        var firstRepository = new QueryTagRepository(qaContext, entityUtils);
        var secondRepository = new QueryTagRepository(qaContext, entityUtils);
        var alternateRepository = new AlternateQueryTagRepository(qaContext, entityUtils);
        var firstToken = firstRepository.CancellationToken;
        var secondToken = secondRepository.CancellationToken;
        var alternateToken = alternateRepository.CancellationToken;

        secondToken.Should().Be(firstToken);
        alternateToken.Should().NotBe(firstToken);
        cancellation.GetOrCreateScope(AlternateQueryTagRepository.ScopeKey).Token.Should().Be(alternateToken);

        firstRepository.CancelChanges();

        firstToken.IsCancellationRequested.Should().BeTrue();
        secondToken.IsCancellationRequested.Should().BeTrue();
        alternateToken.IsCancellationRequested.Should().BeFalse();
        cancellation.Root.IsCancellationRequested.Should().BeFalse();
        await AssertQueryCancellation(secondRepository, secondToken);
    }

    private static void AssertParentServiceScopeIsolation(DrnTestContext context)
    {
        using (var canceledScope = context.CreateScope())
        {
            var canceledRepository = canceledScope.ServiceProvider.GetRequiredService<ITagRepository>();
            canceledRepository.CancelChanges();
            canceledRepository.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }

        using var activeScope = context.CreateScope();
        var activeCancellation = activeScope.ServiceProvider.GetRequiredService<ICancellationUtils>();
        var activeRepository = activeScope.ServiceProvider.GetRequiredService<ITagRepository>();

        activeCancellation.Root.IsCancellationRequested.Should().BeFalse();
        activeRepository.CancellationToken.IsCancellationRequested.Should().BeFalse();
    }

    private static async Task AssertQueryCancellation(ISourceKnownRepository<Tag> repository, CancellationToken effectiveToken)
    {
        repository.CancellationToken.Should().Be(effectiveToken);
        effectiveToken.IsCancellationRequested.Should().BeTrue();

        var query = async () => await repository.AnyAsync();
        await query.Should().ThrowAsync<OperationCanceledException>();
    }

    private static async Task AssertPaginationWithSettingsFilters(ITagRepository repository)
    {
        var prefix = $"{nameof(AssertPaginationWithSettingsFilters)}_{Guid.NewGuid():N}";
        var matchingTags = Enumerable.Range(1, 5)
            .Select(index => TagGenerator.New(prefix, $"match_{index}", value: true, other: index * PaginationFilterMinimum))
            .ToArray();
        var prefixOnlyTags = Enumerable.Range(1, 5)
            .Select(index => TagGenerator.New(prefix, $"prefix_only_{index}", value: false, other: index * 10L))
            .ToArray();
        var valueOnlyTag = TagGenerator.New(
            $"{nameof(AssertPaginationWithSettingsFilters)}_different_{Guid.NewGuid():N}",
            "value_only",
            value: true,
            other: 6 * PaginationFilterMinimum);
        var boolOnlyTag = TagGenerator.New(
            prefix,
            "bool_only",
            value: true,
            other: PaginationFilterMinimum - 1);
        var minimumOnlyTag = TagGenerator.New(
            prefix,
            "minimum_only",
            value: false,
            other: PaginationFilterMinimum);

        AssertMatchesActiveFilters(matchingTags, prefix);
        prefixOnlyTags.Should().OnlyContain(tag =>
            tag.Name.StartsWith(prefix) && (!tag.Model.BoolValue || tag.Model.Other < PaginationFilterMinimum));
        valueOnlyTag.Name.StartsWith(prefix).Should().BeFalse();
        valueOnlyTag.Model.BoolValue.Should().BeTrue();
        valueOnlyTag.Model.Other.Should().BeGreaterThanOrEqualTo(PaginationFilterMinimum);
        boolOnlyTag.Name.StartsWith(prefix).Should().BeTrue();
        boolOnlyTag.Model.BoolValue.Should().BeTrue();
        boolOnlyTag.Model.Other.Should().BeLessThan(PaginationFilterMinimum);
        minimumOnlyTag.Name.StartsWith(prefix).Should().BeTrue();
        minimumOnlyTag.Model.BoolValue.Should().BeFalse();
        minimumOnlyTag.Model.Other.Should().BeGreaterThanOrEqualTo(PaginationFilterMinimum);

        repository.Add(matchingTags
            .Concat(prefixOnlyTags)
            .Append(valueOnlyTag)
            .Append(boolOnlyTag)
            .Append(minimumOnlyTag)
            .ToArray());

        await repository.SaveChangesAsync();

        repository.Settings.AddFilter("PrefixFilter", tag => tag.Name.StartsWith(prefix));
        repository.Settings.AddFilter("MatchingFilter", tag => tag.Model.BoolValue && tag.Model.Other >= PaginationFilterMinimum);
        repository.Settings.Filters.Should().HaveCount(2);

        var page1 = await repository.PaginateAsync(pageSize: 2, direction: PageSortDirection.Ascending, updateTotalCount: true);
        page1.Info.Request.PageCursor.IsFirstRequest.Should().BeTrue();
        AssertPaginationMetadata(
            page1,
            pageNumber: 1,
            itemCount: 2,
            matchingTags.Length,
            hasNext: true,
            hasPrevious: false,
            totalCountUpdated: true);
        AssertMatchesActiveFilters(page1.Items, prefix);

        var page2 = await repository.PaginateAsync(page1.Info, jumpTo: 2, pageSize: 2);
        AssertPaginationMetadata(
            page2,
            pageNumber: 2,
            itemCount: 2,
            matchingTags.Length,
            hasNext: true,
            hasPrevious: true,
            totalCountUpdated: false);
        AssertMatchesActiveFilters(page2.Items, prefix);

        var page3 = await repository.PaginateAsync(page2.Info, jumpTo: 3, pageSize: 2);
        AssertPaginationMetadata(
            page3,
            pageNumber: 3,
            itemCount: 1,
            matchingTags.Length,
            hasNext: false,
            hasPrevious: true,
            totalCountUpdated: false);
        AssertMatchesActiveFilters(page3.Items, prefix);

        var allPaginatedItems = page1.Items.Concat(page2.Items).Concat(page3.Items).ToList();
        AssertTagOrder(allPaginatedItems, matchingTags);
        allPaginatedItems.Should().NotContain(valueOnlyTag);
        allPaginatedItems.Should().NotContain(boolOnlyTag);
        allPaginatedItems.Should().NotContain(minimumOnlyTag);
        allPaginatedItems.Should().NotContain(tag => prefixOnlyTags.Contains(tag));

        var pageDesc1 = await repository.PaginateAsync(pageSize: 2, direction: PageSortDirection.Descending, updateTotalCount: true);
        AssertPaginationMetadata(
            pageDesc1,
            pageNumber: 1,
            itemCount: 2,
            matchingTags.Length,
            hasNext: true,
            hasPrevious: false,
            totalCountUpdated: true);
        AssertMatchesActiveFilters(pageDesc1.Items, prefix);

        var pageDesc2 = await repository.PaginateAsync(pageDesc1.Info, jumpTo: 2, pageSize: 2);
        AssertPaginationMetadata(
            pageDesc2,
            pageNumber: 2,
            itemCount: 2,
            matchingTags.Length,
            hasNext: true,
            hasPrevious: true,
            totalCountUpdated: false);
        AssertMatchesActiveFilters(pageDesc2.Items, prefix);

        var pageDesc3 = await repository.PaginateAsync(pageDesc2.Info, jumpTo: 3, pageSize: 2);
        AssertPaginationMetadata(
            pageDesc3,
            pageNumber: 3,
            itemCount: 1,
            matchingTags.Length,
            hasNext: false,
            hasPrevious: true,
            totalCountUpdated: false);
        AssertMatchesActiveFilters(pageDesc3.Items, prefix);

        var allDescItems = pageDesc1.Items.Concat(pageDesc2.Items).Concat(pageDesc3.Items).ToList();
        AssertTagOrder(allDescItems, matchingTags.Reverse());
        allDescItems.Should().NotContain(boolOnlyTag);
        allDescItems.Should().NotContain(minimumOnlyTag);

        repository.Settings.AddFilter("NoMatchFilter", tag => tag.Model.Other > 99999);
        repository.Settings.Filters.Should().HaveCount(3);
        var emptyPage = await repository.PaginateAsync(pageSize: 2, direction: PageSortDirection.Ascending, updateTotalCount: true);
        emptyPage.Items.Should().BeEmpty();
        AssertPaginationMetadata(
            emptyPage,
            pageNumber: 1,
            itemCount: 0,
            totalCount: 0,
            hasNext: false,
            hasPrevious: false,
            totalCountUpdated: true);

        repository.Settings.RemoveFilter("NoMatchFilter").Should().BeTrue();
        repository.Settings.Filters.Should().HaveCount(2);

        var paginateSingleRequest = PaginationRequest.DefaultWith(2);
        var paginateAllItems = new List<Tag>();
        var paginateAllPageCount = 0;
        await foreach (var pageResult in repository.PaginateAllAsync(paginateSingleRequest))
        {
            paginateAllPageCount++;
            pageResult.Info.Request.PageNumber.Should().Be(paginateAllPageCount);
            pageResult.Items.Should().NotBeEmpty();
            pageResult.Items.Count.Should().BeLessThanOrEqualTo(paginateSingleRequest.PageSize.Size);
            AssertMatchesActiveFilters(pageResult.Items, prefix);
            paginateAllItems.AddRange(pageResult.Items);
        }

        var expectedPageCount =
            (matchingTags.Length + paginateSingleRequest.PageSize.Size - 1) /
            paginateSingleRequest.PageSize.Size;
        paginateAllPageCount.Should().Be(expectedPageCount);
        AssertTagOrder(paginateAllItems, matchingTags);

        repository.Settings.ClearFilters();
    }

    private static void AssertPaginationMetadata(
        PaginationResultModel<Tag> page,
        long pageNumber,
        int itemCount,
        long totalCount,
        bool hasNext,
        bool hasPrevious,
        bool totalCountUpdated)
    {
        page.Info.Request.PageNumber.Should().Be(pageNumber);
        page.Items.Should().HaveCount(itemCount);
        page.Info.ItemCount.Should().Be(itemCount);
        page.Info.Total.Count.Should().Be(totalCount);
        page.Info.HasNext.Should().Be(hasNext);
        page.Info.HasPrevious.Should().Be(hasPrevious);
        page.Info.TotalCountUpdated.Should().Be(totalCountUpdated);
        page.Info.Request.UpdateTotalCount.Should().Be(totalCountUpdated);
    }

    private static void AssertMatchesActiveFilters(IEnumerable<Tag> tags, string prefix)
        => tags.Should().OnlyContain(tag =>
            tag.Name.StartsWith(prefix) &&
            tag.Model.BoolValue &&
            tag.Model.Other >= PaginationFilterMinimum);

    private static void AssertTagOrder(IEnumerable<Tag> actual, IEnumerable<Tag> expected)
        => actual.Select(tag => tag.EntityIdSource)
            .Should().Equal(expected.Select(tag => tag.EntityIdSource));
}
