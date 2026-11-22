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
        controller.Repository.Should().BeSameAs(repository);
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

    [Theory]
    [DataInlineUnit(0)]
    [DataInlineUnit(101)]
    public async Task Invalid_Batches_Should_Be_Rejected_Before_Repository_Work(int count)
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(CreateTagService(repository));
        Func<Task> lookup = () => controller.GetManyAsync(new Guid[count]);
        await lookup.Should().ThrowAsync<ValidationException>();
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
        await controller.PaginateAsync();

        requests.Select(request => request.PageSize.Size).Should().Equal(100, 100, 10);
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
        var categoryService = Substitute.For<ISourceKnownService<CategoryEntity>>();
        categoryService.Repository.Returns(categoryRepo);
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
    public void ControllerBase_Should_Reject_Dto_Response_Types_And_Null_Service_On_Construction()
    {
        var service = CreateTagService(Substitute.For<ISourceKnownRepository<Tag>>());
        Action createBaseDto = () => _ = new DisallowedBaseDtoController(service);
        Action createDerivedDto = () => _ = new DisallowedTagDtoController(service);
        Action createNullService = () => _ = new FoundationProbeController(null!);

        createBaseDto.Should().Throw<InvalidOperationException>()
            .WithMessage("*Dto*");
        createDerivedDto.Should().Throw<InvalidOperationException>()
            .WithMessage("*TagDto*Dto*");
        createNullService.Should().Throw<ArgumentNullException>()
            .WithParameterName("service");
    }

    [Fact]
    public async Task SourceKnownService_Should_Support_Default_Custom_Mismatch_And_CreateEntityAsync()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var entity = new Tag("Canonical tag")
        {
            Model = new TagValueModel { StringValue = "Source", Type = TagType.User }
        };
        var entityId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        entity.EntityIdSource = new SourceKnownEntityId(new SourceKnownId(10, createdAt, 0, 0, 0), entityId, 6, true, false);
        entity.ModifiedAt = createdAt.AddMinutes(5);

        var defaultService = new TagProbeService(repository);
        defaultService.Repository.Should().BeSameAs(repository);
        var dto = defaultService.ToDto(entity);
        dto.Id.Should().Be(entityId);
        dto.CreatedAt.Should().Be(createdAt);
        dto.ModifiedAt.Should().Be(createdAt.AddMinutes(5));
        dto.Name.Should().Be("Canonical tag");

        // Covariant assignment to ISourceKnownService<Tag, Dto, CreateProbeRequest>.
        ISourceKnownService<Tag, Dto, CreateProbeRequest> covariant = defaultService;
        covariant.ToDto(entity).Should().BeOfType<TagDto>().Which.Name.Should().Be("Canonical tag");
        var created = await covariant.CreateEntityAsync(new CreateProbeRequest("Created via service"));
        created.Name.Should().Be("Created via service");

        Action nullDefault = () => defaultService.ToDto(null!);
        nullDefault.Should().Throw<ArgumentNullException>();

        Action nullRepository = () => _ = new TagProbeService(null!);
        nullRepository.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");

        var customService = new CustomTagDtoService(repository);
        customService.ToDto(entity).Name.Should().Be("custom:Canonical tag");

        var mismatchService = new MismatchedTagDtoService(repository);
        Action mismatch = () => mismatchService.ToDto(entity);
        mismatch.Should().Throw<InvalidOperationException>()
            .WithMessage("*TagEntity.ToDto()*TagDto*CategoryDto*");

        Action nullResult = () => defaultService.ToDto(new NullToDtoProbeEntity());
        nullResult.Should().Throw<InvalidOperationException>()
            .WithMessage("*NullToDtoProbeEntity.ToDto()*null*TagDto*");
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
        : SourceKnownService<Tag, TagDto, CreateProbeRequest>(repository)
    {
        public int CreateCalls { get; private set; }

        public override Task<Tag> CreateEntityAsync(CreateProbeRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new Tag(request.Name) { Model = new() });
        }
    }

    private sealed class CustomTagDtoService(ISourceKnownRepository<Tag> repository)
        : SourceKnownService<Tag, TagDto>(repository)
    {
        public override TagDto ToDto(Tag entity) => new(entity)
        {
            Name = "custom:" + entity.Name,
            Model = entity.Model
        };
    }

    private sealed class MismatchedTagDtoService(ISourceKnownRepository<Tag> repository)
        : SourceKnownService<Tag, CategoryDto>(repository);

    private sealed class NullToDtoProbeEntity() : Tag("Null mapping")
    {
        public override TagDto ToDto() => null!;
    }

    private sealed class DisallowedBaseDtoController(ISourceKnownService<Tag> service)
        : SourceKnownControllerBase<Tag, Dto>(service)
    {
        protected override Dto ToResponse(Tag entity) => entity.ToDto();
    }

    private sealed class DisallowedTagDtoController(ISourceKnownService<Tag> service)
        : SourceKnownControllerBase<Tag, TagDto>(service)
    {
        protected override TagDto ToResponse(Tag entity) => entity.ToDto();
    }

    [Route("source-known-foundation")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public sealed class FoundationProbeController(ISourceKnownService<Tag> service)
        : SourceKnownControllerBase<Tag, TagProbeResponse>(service)
    {
        protected override TagProbeResponse ToResponse(Tag entity) => ToProbeResponse(entity);
    }

    [Route("source-known-read")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public class ReadProbeController(ISourceKnownService<Tag> service)
        : SourceKnownReadOnlyController<Tag, TagProbeResponse>(service)
    {
        protected override TagProbeResponse ToResponse(Tag entity) => ToProbeResponse(entity);
    }

    [Route("source-known-disabled")]
    public sealed class DisabledProbeController(ISourceKnownService<Tag> service) : ReadProbeController(service)
    {
        [NonAction]
        public override Task<long> CountAsync(CancellationToken cancellationToken = default) => base.CountAsync(cancellationToken);

        [ApiExplorerSettings(IgnoreApi = true)]
        public override Task<bool> AnyAsync(CancellationToken cancellationToken = default) => base.AnyAsync(cancellationToken);
    }

    [Route("source-known-write")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public class WriteProbeController(ISourceKnownService<Tag, Dto, CreateProbeRequest> service)
        : SourceKnownController<Tag, TagProbeResponse, CreateProbeRequest>(service)
    {
        public bool AllowWrite { get; set; }
        [Sensitive(false, Justification = "Test invocation count. Contains no authorization data or credentials.")]
        public int AuthorizationCalls { get; private set; }

        protected override TagProbeResponse ToResponse(Tag entity) => ToProbeResponse(entity);

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
    private sealed class StrictCategoryProbeController(ISourceKnownService<CategoryEntity> service)
        : SourceKnownReadOnlyController<CategoryEntity, CategoryResponse>(service)
    {
        protected override CategoryResponse ToResponse(CategoryEntity entity) => new()
        {
            Id = entity.EntityId,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            Name = entity.Name
        };
    }

    [Route("source-known-disabled-write")]
    public sealed class DisabledWriteProbeController(ISourceKnownService<Tag, Dto, CreateProbeRequest> service) : WriteProbeController(service)
    {
        [NonAction]
        public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => base.DeleteAsync(id, cancellationToken);
    }

    private sealed class CancelMappingProbeController(ISourceKnownService<Tag, Dto, CreateProbeRequest> service, CancellationTokenSource cancellation)
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
    public sealed class WriteOnlyProbeController(ISourceKnownService<Tag, Dto, CreateProbeRequest> service)
        : SourceKnownWriteOnlyController<Tag, TagProbeResponse, CreateProbeRequest>(service)
    {
        protected override TagProbeResponse ToResponse(Tag entity) => ToProbeResponse(entity);

        protected override Task<Tag> CreateEntityAsync(CreateProbeRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("Discovery fixture does not allow writes.");
    }

    private static TagProbeResponse ToProbeResponse(Tag entity) => new()
    {
        Id = entity.EntityId,
        CreatedAt = entity.CreatedAt,
        ModifiedAt = entity.ModifiedAt,
        Name = entity.Name
    };
}
