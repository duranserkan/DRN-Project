using DRN.Framework.Hosting.Controllers;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Controllers;

public class SourceKnownControllerTests
{
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
        readActions.Select(action => action.MethodInfo.Name).Should().BeEquivalentTo(new[]
        {
            "GetAsync", "GetManyAsync", "AnyAsync", "CountAsync", "PaginateAsync", "PaginateWithOptionsAsync", "PaginateWithBodyAsync"
        });
        var writeActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(WriteProbeController)).ToArray();
        writeActions.Should().HaveCount(9);
        writeActions.Should().OnlyContain(action => action.EndpointMetadata.OfType<AuthorizeAttribute>().Any(attribute => attribute.Policy == "SourceKnown.Probe"));
        foreach (var action in writeActions.Where(action => action.MethodInfo.Name is "PostAsync" or "DeleteAsync"))
            action.FilterDescriptors.Should().NotContain(filter => filter.Filter is ValidateAntiForgeryTokenAttribute);

        var writeOnlyActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(WriteOnlyProbeController)).ToArray();
        writeOnlyActions.Select(action => action.MethodInfo.Name).Should().BeEquivalentTo(new[] { "PostAsync", "DeleteAsync" });
        var writeOnlyDescriptions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(description => ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo.AsType() == typeof(WriteOnlyProbeController))
            .ToArray();
        writeOnlyDescriptions.Select(description => description.HttpMethod).Should().BeEquivalentTo(new[] { "POST", "DELETE" });
        writeOnlyDescriptions.Select(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name)
            .Should().BeEquivalentTo(new[] { "PostAsync", "DeleteAsync" });

        var disabledActions = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(DisabledProbeController)).ToArray();
        disabledActions.Should().NotContain(action => action.MethodInfo.Name == "CountAsync");
        disabledActions.Should().Contain(action => action.MethodInfo.Name == "AnyAsync");
        disabledActions.Should().OnlyContain(action => action.AttributeRouteInfo != null);

        var descriptions = provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(description => ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo.AsType() == typeof(DisabledProbeController))
            .ToArray();
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "CountAsync");
        descriptions.Should().NotContain(description => ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "AnyAsync");
        descriptions.Should().HaveCount(5);

        var disabledWrites = actions.Where(action => action.ControllerTypeInfo.AsType() == typeof(DisabledWriteProbeController)).ToArray();
        disabledWrites.Should().HaveCount(8);
        disabledWrites.Should().NotContain(action => action.MethodInfo.Name == "DeleteAsync");
        disabledWrites.Should().Contain(action => action.MethodInfo.Name == "PostAsync");
        provider.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(group => group.Items).Should().NotContain(description =>
                ((ControllerActionDescriptor)description.ActionDescriptor).ControllerTypeInfo.AsType() == typeof(DisabledWriteProbeController) &&
                ((ControllerActionDescriptor)description.ActionDescriptor).MethodInfo.Name == "DeleteAsync");
    }

    [Fact]
    public async Task Reads_Should_Map_Dtos_Preserve_Default_Format_And_Link_Cancellation()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(repository);
        var tag = new Tag("Visible tag") { Model = new() };
        var id = Guid.NewGuid();
        Guid[] ids = [id];
        repository.GetAsync(id, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(tag);
        repository.GetAsync(ids, SourceKnownEntityIdFormat.ConfiguredDefault).Returns(new[] { tag });
        repository.AnyAsync().Returns(true);
        repository.CountAsync().Returns(17L);
        using var cancellation = new CancellationTokenSource();

        controller.Repository.Should().BeSameAs(repository);
        (await controller.GetAsync(id, cancellation.Token)).Name.Should().Be(tag.Name);
        (await controller.GetManyAsync(ids, cancellation.Token)).Select(dto => dto.Name).Should().Equal(tag.Name);
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
        var controller = new ReadProbeController(repository);
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
        var controller = new ReadProbeController(repository);
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
        result.Items.Select(dto => dto.Name).Should().Equal(tag.Name);
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
        var controller = new ReadProbeController(repository);
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
        result.Items.Select(dto => dto.Name).Should().Equal(items.Take(2).Select(item => item.Name));
    }

    [Fact]
    public async Task All_Pagination_Forms_Should_Use_The_Server_Ceiling()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new ReadProbeController(repository);
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
    public async Task Application_Overrides_Should_Reject_Writes_Before_Mapping_And_Mutation()
    {
        var repository = Substitute.For<ISourceKnownRepository<Tag>>();
        var controller = new WriteProbeController(repository);
        var request = new CreateProbeRequest("Created tag");
        var id = Guid.NewGuid();
        Func<Task> create = () => controller.PostAsync(request);
        Func<Task> delete = () => controller.DeleteAsync(id);

        await create.Should().ThrowAsync<ForbiddenException>();
        await delete.Should().ThrowAsync<ForbiddenException>();
        controller.MappingCalls.Should().Be(0);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<Tag>>());
        await repository.DidNotReceive().DeleteAsync(Arg.Any<IReadOnlyCollection<Guid>>());

        controller.AllowWrite = true;
        var response = await controller.PostAsync(request);
        var created = response.Result.Should().BeOfType<ObjectResult>().Subject;
        created.StatusCode.Should().Be(201);
        created.Value.Should().BeOfType<TagDto>().Which.Name.Should().Be(request.Name);
        controller.MappingCalls.Should().Be(1);
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
        using var cancellation = new CancellationTokenSource();
        var controller = new CancelMappingProbeController(repository, cancellation) { AllowWrite = true };
        Func<Task> create = () => controller.PostAsync(new CreateProbeRequest("Canceled tag"), cancellation.Token);

        await create.Should().ThrowAsync<OperationCanceledException>();

        repository.Received(1).CancelWhen(cancellation.Token);
        controller.MappingCalls.Should().Be(1);
        await repository.DidNotReceive().CreateAsync(Arg.Any<IReadOnlyCollection<Tag>>());
    }

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

    [Route("source-known-foundation")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public sealed class FoundationProbeController(ISourceKnownRepository<Tag> repository)
        : SourceKnownControllerBase<Tag, TagDto>(repository)
    {
        protected override TagDto ToDto(Tag entity) => new() { Name = entity.Name, Model = entity.Model };
    }

    [Route("source-known-read")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public class ReadProbeController(ISourceKnownRepository<Tag> repository)
        : SourceKnownReadOnlyController<Tag, TagDto>(repository)
    {
        protected override TagDto ToDto(Tag entity) => new() { Name = entity.Name, Model = entity.Model };
    }

    [Route("source-known-disabled")]
    public sealed class DisabledProbeController(ISourceKnownRepository<Tag> repository) : ReadProbeController(repository)
    {
        [NonAction]
        public override Task<long> CountAsync(CancellationToken cancellationToken = default) => base.CountAsync(cancellationToken);

        [ApiExplorerSettings(IgnoreApi = true)]
        public override Task<bool> AnyAsync(CancellationToken cancellationToken = default) => base.AnyAsync(cancellationToken);
    }

    public sealed record CreateProbeRequest(string Name);

    [Route("source-known-write")]
    [Authorize(Policy = "SourceKnown.Probe")]
    public class WriteProbeController(ISourceKnownRepository<Tag> repository)
        : SourceKnownController<Tag, TagDto, CreateProbeRequest>(repository)
    {
        public bool AllowWrite { get; set; }
        public int MappingCalls { get; private set; }
        [Sensitive(false, Justification = "Test invocation count. Contains no authorization data or credentials.")]
        public int AuthorizationCalls { get; private set; }
        protected override TagDto ToDto(Tag entity) => new() { Name = entity.Name, Model = entity.Model };

        public override Task<ActionResult<TagDto>> PostAsync(CreateProbeRequest request, CancellationToken cancellationToken = default)
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

        protected override Task<Tag> CreateEntityAsync(CreateProbeRequest request, CancellationToken cancellationToken)
        {
            MappingCalls++;
            return Task.FromResult(new Tag(request.Name) { Model = new() });
        }

        private void EnsureWriteAllowed()
        {
            AuthorizationCalls++;
            if (!AllowWrite)
                throw ExceptionFor.Forbidden("Write not allowed.");
        }
    }

    [Route("source-known-disabled-write")]
    public sealed class DisabledWriteProbeController(ISourceKnownRepository<Tag> repository) : WriteProbeController(repository)
    {
        [NonAction]
        public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => base.DeleteAsync(id, cancellationToken);
    }

    private sealed class CancelMappingProbeController(ISourceKnownRepository<Tag> repository, CancellationTokenSource cancellation)
        : WriteProbeController(repository)
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
    public sealed class WriteOnlyProbeController(ISourceKnownRepository<Tag> repository)
        : SourceKnownWriteOnlyController<Tag, TagDto, CreateProbeRequest>(repository)
    {
        protected override TagDto ToDto(Tag entity) => new() { Name = entity.Name, Model = entity.Model };

        protected override Task<Tag> CreateEntityAsync(CreateProbeRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException("Discovery fixture does not allow writes.");
    }
}
