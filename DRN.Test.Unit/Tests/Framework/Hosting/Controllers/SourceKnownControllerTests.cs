using DRN.Framework.Hosting.Controllers;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Test.Utils.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Sample.Contract.QA.Categories;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Category;
using Tag = Sample.Domain.QA.Tag.TagEntity;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Controllers;

public class SourceKnownControllerTests
{
    private static readonly string[] ReadActionNames =
    [
        "GetAsync", "GetManyAsync", "AnyAsync", "CountAsync", "PaginateAsync", "PaginateWithOptionsAsync", "PaginateWithBodyAsync"
    ];
    private static readonly string[] WriteActionNames = ["PostAsync", "DeleteAsync"];
    private static readonly string[] WriteHttpMethods = ["POST", "DELETE"];

    [Fact]
    public void MVC_Should_Discover_Inherited_Actions_And_Remove_NonActions_Without_Confusing_IgnoreApi()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.FeatureProviders.Clear();
            manager.FeatureProviders.Add(new ProbeFeatureProvider());
        });
        using var provider = services.BuildServiceProvider();
        var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>().ToArray();

        actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(FoundationProbeController)).Should().BeEmpty();
        var readActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(ReadProbeController)).ToArray();
        readActions.Select(action => action.MethodInfo.Name).Should().BeEquivalentTo(ReadActionNames);
        var writeActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(WriteProbeController)).ToArray();
        writeActions.Should().HaveCount(9);
        writeActions.Should().OnlyContain(action => action.EndpointMetadata.OfType<AuthorizeAttribute>().Any(attribute => attribute.Policy == "SourceKnown.Probe"));
        foreach (var action in writeActions.Where(action => action.MethodInfo.Name is "PostAsync" or "DeleteAsync"))
            action.FilterDescriptors.Should().NotContain(filter => filter.Filter is ValidateAntiForgeryTokenAttribute);

        var writeOnlyActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(WriteOnlyProbeController)).ToArray();
        writeOnlyActions.Select(action => action.MethodInfo.Name).Should().BeEquivalentTo(WriteActionNames);
        var controllerDescriptions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(description => description.ActionDescriptor is ControllerActionDescriptor)
            .ToArray();
        var writeOnlyDescriptions = controllerDescriptions
            .Where(description => ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo.AsType() == typeof(WriteOnlyProbeController))
            .ToArray();
        writeOnlyDescriptions.Select(description => description.HttpMethod).Should().BeEquivalentTo(WriteHttpMethods);
        writeOnlyDescriptions.Select(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name)
            .Should().BeEquivalentTo(WriteActionNames);

        var disabledActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(DisabledProbeController)).ToArray();
        disabledActions.Should().NotContain(action => action.MethodInfo.Name == "CountAsync");
        disabledActions.Should().Contain(action => action.MethodInfo.Name == "AnyAsync");
        disabledActions.Should().OnlyContain(action => action.AttributeRouteInfo != null);

        var descriptions = controllerDescriptions
            .Where(description => ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo.AsType() == typeof(DisabledProbeController))
            .ToArray();
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "CountAsync");
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "AnyAsync");
        descriptions.Should().HaveCount(5);

        var disabledWrites = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(DisabledWriteProbeController)).ToArray();
        disabledWrites.Should().HaveCount(8);
        disabledWrites.Should().NotContain(action => action.MethodInfo.Name == "DeleteAsync");
        disabledWrites.Should().Contain(action => action.MethodInfo.Name == "PostAsync");
        controllerDescriptions.Should().NotContain(description =>
                ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo.AsType() == typeof(DisabledWriteProbeController) &&
                ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "DeleteAsync");
    }

    [Fact]
    public async Task Reads_Should_Map_Responses_Preserve_Default_Format_And_Link_Cancellation()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var service = CreateTagService(repository);
        var controller = new ReadProbeController(service);
        var tag = new Tag("Visible tag") { Model = new() };
        var id = Guid.NewGuid();
        Guid[] ids = [id];
        repository.GetAsync(id, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(tag);
        repository.GetAsync(ids, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(new[] { tag });
        repository.AnyAsync().Returns(true);
        repository.CountAsync().Returns(17L);
        using var cancellation = new CancellationTokenSource();

        controller.Should().NotBeAssignableTo<ISourceKnownService<Tag>>();
        typeof(SourceKnownControllerBase<Tag, TagProbeResponse>)
            .GetProperty("Repository", BindingFlags.Instance | BindingFlags.Public).Should().BeNull();
        controller.ExposedRepository.Should().BeSameAs(repository);
        (await controller.GetAsync(id, cancellation.Token)).Name.Should().Be(tag.Name);
        (await controller.GetManyAsync(ids, cancellation.Token)).Select(response => response.Name).Should().Equal(tag.Name);
        (await controller.AnyAsync(cancellation.Token)).Should().BeTrue();
        (await controller.CountAsync(cancellation.Token)).Should().Be(17);
        repository.Received(4).CancelWhen(cancellation.Token);
        await repository.Received(1).GetAsync(id, SourceKnownEntityIdFormat.ConfiguredDefault);

        repository.ClearReceivedCalls();
        cancellation.Cancel();
        Func<Task> lookup = () => controller.GetAsync(id, cancellation.Token);
        await lookup.Should().ThrowAsync<OperationCanceledException>();
        repository.ReceivedCalls().Should().BeEmpty();
    }

    public static IEnumerable<object?[]> InvalidLookupBatches()
    {
        yield return [null];
        yield return [Array.Empty<Guid>()];
        yield return [new Guid[101]];
    }

    [Theory]
    [DataMemberUnit(nameof(InvalidLookupBatches))]
    public async Task Invalid_Batches_Should_Be_Rejected_Before_Repository_Work(Guid[]? ids)
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(CreateTagService(repository));
        Func<Task> lookup = () => controller.GetManyAsync(ids!);
        await lookup.Should().ThrowAsync<ValidationException>()
            .WithMessage("Provide a nonempty ID batch within the server's batch limit.");
        repository.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(true, 1L, long.MaxValue, 1L)]
    [DataInlineUnit(false, 5L, long.MaxValue, 15L)]
    [DataInlineUnit(false, 50L, 1L, 40L)]
    [DataInlineUnit(false, long.MaxValue - 1, long.MaxValue, long.MaxValue)]
    public async Task Pagination_Should_Bound_Jumps_And_Discard_Client_Counts(
        bool firstRequest, long cursorPage, long requestedPage, long expectedPage)
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(CreateTagService(repository));
        var tag = new Tag("Page item") { Model = new() };
        PaginationRequest? actualRequest = null;
        repository.PaginateAsync(Arg.Any<PaginationRequest>(), null).Returns(call =>
        {
            actualRequest = call.ArgAt<PaginationRequest>(0);
            var info = new PaginationResultInfo(actualRequest, Guid.Empty, Guid.Empty, 1, false, false, PaginationTotal.NotSpecified);
            return new PaginationResultModel<Tag>(info, [tag]);
        });
        var cursor = firstRequest ? PageCursor.Initial : new PageCursor(cursorPage, Guid.NewGuid(), Guid.NewGuid());
        var request = new PaginationRequest(requestedPage, new PageSize(1000, 1000), cursor,
            totalCount: 987654, updateTotalCount: true, markAsHasNextOnRefresh: true);

        var result = await controller.PaginateWithBodyAsync(request);

        actualRequest.Should().NotBeNull();
        actualRequest!.PageNumber.Should().Be(expectedPage);
        actualRequest.PageSize.Size.Should().Be(100);
        actualRequest.PageSize.MaxSize.Should().Be(100);
        actualRequest.TotalCount.Should().Be(-1);
        actualRequest.UpdateTotalCount.Should().BeTrue();
        actualRequest.MarkAsHasNextOnRefresh.Should().BeFalse();
        result.Items.Select(response => response.Name).Should().Equal(tag.Name);
        result.Info.Request.Should().BeSameAs(actualRequest);
    }

    [Theory]
    [DataInlineUnit(PageSortDirection.Ascending, 0, true, false)]
    [DataInlineUnit(PageSortDirection.Ascending, 2, true, false)]
    [DataInlineUnit(PageSortDirection.Ascending, 3, false, true)]
    [DataInlineUnit(PageSortDirection.Descending, 0, true, false)]
    [DataInlineUnit(PageSortDirection.Descending, 2, true, false)]
    [DataInlineUnit(PageSortDirection.Descending, 3, false, true)]
    public async Task Refresh_Should_Return_Server_Derived_HasNext_After_Discarding_Client_Hints(
        PageSortDirection direction, int itemCount, bool clientHasNext, bool expectedHasNext)
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(CreateTagService(repository));
        Tag[] availableItems = [new("First") { Model = new() }, new("Second") { Model = new() }, new("Third") { Model = new() }];
        var items = availableItems.Take(itemCount).ToArray();
        repository.PaginateAsync(Arg.Any<PaginationRequest>(), null).Returns(call =>
            new PaginationResult<Tag>(items, call.ArgAt<PaginationRequest>(0)).ToModel());
        var previous = new PaginationResultInfo(
            new PaginationRequest(2, new PageSize(2), PageCursor.InitialWith(direction)),
            Guid.NewGuid(), Guid.NewGuid(), 2, clientHasNext, true, PaginationTotal.NotSpecified);

        var result = await controller.PaginateWithBodyAsync(previous.RequestRefresh());

        result.Info.Request.IsPageRefresh.Should().BeTrue();
        result.Info.Request.MarkAsHasNextOnRefresh.Should().BeFalse();
        result.Info.Request.TotalCount.Should().Be(-1);
        result.Info.HasNext.Should().Be(expectedHasNext);
        result.Info.Total.CountSpecified.Should().BeFalse();
        result.Items.Select(response => response.Name).Should().Equal(items.Take(2).Select(item => item.Name));
    }

    [Fact]
    public async Task All_Pagination_Forms_Should_Use_The_Server_Ceiling()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(CreateTagService(repository));
        var requests = new List<PaginationRequest>();
        repository.PaginateAsync(Arg.Any<PaginationRequest>(), null).Returns(call =>
        {
            var request = call.ArgAt<PaginationRequest>(0);
            requests.Add(request);
            return new PaginationResultModel<Tag>(
                new PaginationResultInfo(request, Guid.Empty, Guid.Empty, 0, false, false, PaginationTotal.NotSpecified), []);
        });

        await controller.PaginateAsync(new PaginationRequest(1, new PageSize(1000, 1000)));
        await controller.PaginateWithOptionsAsync(new PaginationOptions { PageSize = 1000, MaxSize = 1000 });
        await controller.PaginateWithOptionsAsync(null);
        await controller.PaginateAsync();

        requests.Select(request => request.PageSize.Size).Should().Equal(100, 100, 10, 10);
        requests.Should().OnlyContain(request => request.PageSize.MaxSize == 100 && request.TotalCount == -1);
    }

    [Fact]
    public async Task Application_Overrides_Should_Reject_Writes_Before_Service_Mapping_And_Mutation()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var service = CreateTagService(repository);
        var controller = new WriteProbeController(service);
        var request = new CreateProbeRequest("Created tag");
        var id = Guid.NewGuid();
        Func<Task> create = () => controller.PostAsync(request);
        Func<Task> delete = () => controller.DeleteAsync(id);

        await create.Should().ThrowAsync<ForbiddenException>();
        await delete.Should().ThrowAsync<ForbiddenException>();
        service.CreateCalls.Should().Be(0);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<Tag>>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<IReadOnlyCollection<Guid>>());

        controller.AllowWrite = true;
        var response = await controller.PostAsync(request);
        var created = response.Result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.Value.Should().BeOfType<TagProbeResponse>().Which.Name.Should().Be(request.Name);
        service.CreateCalls.Should().Be(1);
        await repository.Received(1).CreateAsync(Arg.Is<IReadOnlyCollection<Tag>>(entities => entities.Single().Name == request.Name));
        (await controller.DeleteAsync(id)).StatusCode.Should().Be(204);
        await repository.Received(1).DeleteAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == id));

        var authorizationCalls = controller.AuthorizationCalls;
        repository.ClearReceivedCalls();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> canceledCreate = () => controller.PostAsync(request, cancellation.Token);
        await canceledCreate.Should().ThrowAsync<OperationCanceledException>();
        controller.AuthorizationCalls.Should().Be(authorizationCalls);
        repository.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Cancellation_During_Mapping_Should_Prevent_Persistence()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var service = CreateTagService(repository);
        using var cancellation = new CancellationTokenSource();
        var controller = new CancelMappingProbeController(service, cancellation) { AllowWrite = true };
        Func<Task> create = () => controller.PostAsync(new CreateProbeRequest("Canceled tag"), cancellation.Token);

        await create.Should().ThrowAsync<OperationCanceledException>();

        repository.Received(1).CancelWhen(cancellation.Token);
        service.CreateCalls.Should().Be(1);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<Tag>>());
    }

    [Fact]
    public async Task Strict_Response_And_Routing_Controllers_Should_Map_Explicit_Responses()
    {
        var categoryRepo = Substitute.For<ISourceKnownRepository<CategoryEntity>>();
        var categoryService = new StrictCategoryProbeService(categoryRepo);
        var category = new CategoryEntity("Strict category");
        var categoryId = Guid.NewGuid();
        category.EntityIdSource = new SourceKnownEntityId(new SourceKnownId(11, DateTimeOffset.UnixEpoch, 0, 0, 0), categoryId, 3, true, false);
        categoryRepo.GetAsync(categoryId, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(category);

        var strictController = new StrictCategoryProbeController(categoryService);
        var response = await strictController.GetAsync(categoryId);
        response.Should().BeOfType<CategoryResponse>();
        response.Id.Should().Be(categoryId);
        response.Name.Should().Be("Strict category");

        var routingRepo = Substitute.For<ISourceKnownRepository<SourceKnownRoutingEntity>>();
        var routingService = new SourceKnownRoutingService(routingRepo);
        var routingEntity = new SourceKnownRoutingEntity { Label = "Routed" };
        var routingId = Guid.NewGuid();
        routingEntity.EntityIdSource = new SourceKnownEntityId(new SourceKnownId(12, DateTimeOffset.UnixEpoch, 0, 127, 0), routingId, 1, true, false);
        routingRepo.GetAsync(routingId, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(routingEntity);

        var routingController = new SourceKnownRoutingController(routingService);
        var writableRoutingController = new SourceKnownWritableRoutingController(routingService);
        (await routingController.GetAsync(routingId)).Should().BeOfType<SourceKnownRoutingResponse>()
            .Which.Id.Should().Be(routingId);
        (await writableRoutingController.GetAsync(routingId)).Should().BeOfType<SourceKnownRoutingResponse>()
            .Which.Label.Should().Be("Routed");
    }

    [Fact]
    public void Services_Should_Reject_Null_Repository_On_Construction()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var service = CreateTagService(repository);
        Action createNullRepository = () => _ = new TagProbeService(null!);

        createNullRepository.Should().Throw<ArgumentNullException>().WithParameterName("repository");
        service.Repository.Should().BeSameAs(repository);
    }

    [Fact]
    public async Task SourceKnownService_Should_Support_CreateResponse_Default_Batch_Custom_Batch_And_Null_Guards()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var first = new Tag("First tag") { Model = new TagValueModel { StringValue = "A", Type = TagType.User } };
        var second = new Tag("Second tag") { Model = new TagValueModel { StringValue = "B", Type = TagType.User } };
        var entityId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        first.EntityIdSource = new SourceKnownEntityId(new SourceKnownId(10, createdAt, 0, 0, 0), entityId, 6, true, false);
        first.ModifiedAt = createdAt.AddMinutes(5);

        var service = new TagProbeService(repository);
        service.Repository.Should().BeSameAs(repository);
        var response = await service.CreateResponseAsync(first);
        response.Id.Should().Be(entityId);
        response.CreatedAt.Should().Be(createdAt);
        response.ModifiedAt.Should().Be(createdAt.AddMinutes(5));
        response.Name.Should().Be("First tag");

        var batch = await service.CreateResponsesAsync([first, second]);
        batch.Select(item => item.Name).Should().Equal("First tag", "Second tag");
        (await service.CreateResponsesAsync([])).Should().BeEmpty();

        var created = await service.CreateEntityAsync(new CreateProbeRequest("Created via service"));
        created.Name.Should().Be("Created via service");

        Func<Task> nullBatch = () => service.CreateResponsesAsync(null!);
        Func<Task> nullElement = () => service.CreateResponsesAsync([first, null!]);
        await nullBatch.Should().ThrowAsync<ArgumentNullException>();
        await nullElement.Should().ThrowAsync<ArgumentNullException>();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> canceledBatch = () => service.CreateResponsesAsync([], cancellation.Token);
        await canceledBatch.Should().ThrowAsync<OperationCanceledException>();

        var nullReturningService = new NullResponseProbeService(repository);
        Func<Task> nullBatchItem = () => nullReturningService.CreateResponsesAsync([first]);
        await nullBatchItem.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CreateResponseAsync*null*");

        var batchedService = new BatchedTagProbeService(repository);
        var id = Guid.NewGuid();
        repository.GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), SourceKnownEntityIdFormat.ConfiguredDefault).Returns([first, second]);
        repository.PaginateAsync(Arg.Any<PaginationRequest>(), null).Returns(call =>
            new PaginationResultModel<Tag>(
                new PaginationResultInfo(call.ArgAt<PaginationRequest>(0), Guid.Empty, Guid.Empty, 2, false, false, PaginationTotal.NotSpecified),
                [first, second]));
        var batchedController = new ReadProbeController(batchedService);

        (await batchedController.GetManyAsync([id])).Select(item => item.Name).Should().Equal("batch:First tag", "batch:Second tag");
        (await batchedController.PaginateAsync()).Items.Select(item => item.Name).Should().Equal("batch:First tag", "batch:Second tag");
        batchedService.BatchCalls.Should().Be(2);
        batchedService.SingleCalls.Should().Be(0);

        var nullGuardService = Substitute.For<ISourceKnownService<Tag, TagProbeResponse, CreateProbeRequest>>();
        nullGuardService.Repository.Returns(repository);
        nullGuardService.CreateEntityAsync(Arg.Any<CreateProbeRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<Tag>(null!));
        nullGuardService.CreateResponseAsync(Arg.Any<Tag>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<TagProbeResponse>(null!));
        nullGuardService.CreateResponsesAsync(Arg.Any<IReadOnlyList<Tag>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<TagProbeResponse[]>(null!));
        repository.GetAsync(id, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(first);
        var nullGuardController = new WriteProbeController(nullGuardService) { AllowWrite = true };

        Func<Task> nullPost = () => nullGuardController.PostAsync(null!);
        Func<Task> nullPaginate = () => nullGuardController.PaginateWithBodyAsync(null!);
        await nullPost.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
        await nullPaginate.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");

        Func<Task> nullEntityPost = () => nullGuardController.PostAsync(new CreateProbeRequest("Null entity"));
        Func<Task> nullResponseGet = () => nullGuardController.GetAsync(id);
        Func<Task> nullResponsesBatch = () => nullGuardController.GetManyAsync([id]);
        Func<Task> nullResponsesPage = () => nullGuardController.PaginateAsync();
        await nullEntityPost.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CreateEntityAsync*null*");
        await nullResponseGet.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CreateResponseAsync*null*");
        await nullResponsesBatch.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CreateResponsesAsync*null*");
        await nullResponsesPage.Should().ThrowAsync<InvalidOperationException>().WithMessage("*CreateResponsesAsync*null*");
    }

    private static TagProbeService CreateTagService(ISourceKnownRepository<Tag> repository) => new(repository);

    private sealed class ProbeFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(FoundationProbeController).GetTypeInfo());
            feature.Controllers.Add(typeof(ReadProbeController).GetTypeInfo());
            feature.Controllers.Add(typeof(WriteProbeController).GetTypeInfo());
            feature.Controllers.Add(typeof(DisabledProbeController).GetTypeInfo());
            feature.Controllers.Add(typeof(DisabledWriteProbeController).GetTypeInfo());
            feature.Controllers.Add(typeof(WriteOnlyProbeController).GetTypeInfo());
        }
    }

    public sealed record CreateProbeRequest(string Name);

    public sealed class TagProbeResponse
    {
        public Guid Id { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ModifiedAt { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private sealed class TagProbeService(ISourceKnownRepository<Tag> repository)
        : SourceKnownService<Tag, TagProbeResponse, CreateProbeRequest>(repository)
    {
        public int CreateCalls { get; private set; }

        public override Task<TagProbeResponse> CreateResponseAsync(Tag entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ToProbeResponse(entity));
        }

        public override Task<Tag> CreateEntityAsync(CreateProbeRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new Tag(request.Name) { Model = new() });
        }
    }

    private sealed class BatchedTagProbeService(ISourceKnownRepository<Tag> repository)
        : SourceKnownService<Tag, TagProbeResponse>(repository)
    {
        public int SingleCalls { get; private set; }
        public int BatchCalls { get; private set; }

        public override Task<TagProbeResponse> CreateResponseAsync(Tag entity, CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return Task.FromResult(ToProbeResponse(entity));
        }

        public override Task<TagProbeResponse[]> CreateResponsesAsync(IReadOnlyList<Tag> entities, CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            return Task.FromResult(entities.Select(entity => new TagProbeResponse
            {
                Id = entity.EntityId,
                CreatedAt = entity.CreatedAt,
                ModifiedAt = entity.ModifiedAt,
                Name = "batch:" + entity.Name
            }).ToArray());
        }
    }

    private sealed class NullResponseProbeService(ISourceKnownRepository<Tag> repository)
        : SourceKnownService<Tag, TagProbeResponse>(repository)
    {
        public override Task<TagProbeResponse> CreateResponseAsync(Tag entity, CancellationToken cancellationToken = default)
            => Task.FromResult<TagProbeResponse>(null!);
    }

    private sealed class StrictCategoryProbeService(ISourceKnownRepository<CategoryEntity> repository)
        : SourceKnownService<CategoryEntity, CategoryResponse>(repository)
    {
        public override Task<CategoryResponse> CreateResponseAsync(CategoryEntity entity, CancellationToken cancellationToken = default)
            => Task.FromResult(new CategoryResponse
            {
                Id = entity.EntityId,
                CreatedAt = entity.CreatedAt,
                ModifiedAt = entity.ModifiedAt,
                Name = entity.Name
            });
    }

    [Route("source-known-foundation")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public sealed class FoundationProbeController(ISourceKnownService<Tag, TagProbeResponse> service)
        : SourceKnownControllerBase<Tag, TagProbeResponse>(service);

    [Route("source-known-read")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public class ReadProbeController(ISourceKnownService<Tag, TagProbeResponse> service)
        : SourceKnownReadOnlyController<Tag, TagProbeResponse>(service)
    {
        internal ISourceKnownRepository<Tag> ExposedRepository => Repository;
    }

    [Route("source-known-disabled")]
    public sealed class DisabledProbeController(ISourceKnownService<Tag, TagProbeResponse> service) : ReadProbeController(service)
    {
        [NonAction]
        public override Task<long> CountAsync(CancellationToken cancellationToken = default) => base.CountAsync(cancellationToken);

        [ApiExplorerSettings(IgnoreApi = true)]
        public override Task<bool> AnyAsync(CancellationToken cancellationToken = default) => base.AnyAsync(cancellationToken);
    }

    [Route("source-known-write")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public class WriteProbeController(ISourceKnownService<Tag, TagProbeResponse, CreateProbeRequest> service)
        : SourceKnownController<Tag, TagProbeResponse, CreateProbeRequest>(service)
    {
        public bool AllowWrite { get; set; }
        [Sensitive(false, Justification = "Test invocation count. Contains no authorization data or credentials.")]
        public int AuthorizationCalls { get; private set; }

        public override Task<ActionResult<TagProbeResponse>> PostAsync(CreateProbeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWriteAllowed();
            return base.PostAsync(request, cancellationToken);
        }

        public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureWriteAllowed();
            return base.DeleteAsync(id, cancellationToken);
        }

        private void EnsureWriteAllowed()
        {
            AuthorizationCalls++;
            if (!AllowWrite)
                throw ExceptionFor.Forbidden("Write not allowed.");
        }
    }

    [Route("source-known-strict")]
    [Authorize(Policy = "SourceKnown.Probe")]
    private sealed class StrictCategoryProbeController(ISourceKnownService<CategoryEntity, CategoryResponse> service)
        : SourceKnownReadOnlyController<CategoryEntity, CategoryResponse>(service);

    [Route("source-known-disabled-write")]
    public sealed class DisabledWriteProbeController(ISourceKnownService<Tag, TagProbeResponse, CreateProbeRequest> service) : WriteProbeController(service)
    {
        [NonAction]
        public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => base.DeleteAsync(id, cancellationToken);
    }

    private sealed class CancelMappingProbeController(ISourceKnownService<Tag, TagProbeResponse, CreateProbeRequest> service, CancellationTokenSource cancellation)
        : WriteProbeController(service)
    {
        protected override async Task<Tag> CreateEntityAsync(CreateProbeRequest request, CancellationToken cancellationToken)
        {
            var entity = await base.CreateEntityAsync(request, cancellationToken);
            cancellation.Cancel();
            return entity;
        }
    }

    [Route("source-known-write-only")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public sealed class WriteOnlyProbeController(ISourceKnownService<Tag, TagProbeResponse, CreateProbeRequest> service)
        : SourceKnownWriteOnlyController<Tag, TagProbeResponse, CreateProbeRequest>(service);

    private static TagProbeResponse ToProbeResponse(Tag entity) => new()
    {
        Id = entity.EntityId,
        CreatedAt = entity.CreatedAt,
        ModifiedAt = entity.ModifiedAt,
        Name = entity.Name
    };
}
