using DRN.Framework.Hosting.Controllers;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Test.Utils.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Test.Utils.Hosting;

public static class SourceKnownRoutingPolicies
{
    public const string Read = "SourceKnown.Read";
    public const string Write = "SourceKnown.Write";
    public const string WriteClaim = "source-known-write";
}

[FixtureEntityType<TestApp>(FixtureEntityTypes.Routing)]
public sealed class SourceKnownRoutingEntity : AggregateRoot
{
    public string Label { get; init; } = string.Empty;

    public override SourceKnownRoutingDto ToDto() => new(this) { Label = Label };
}

public sealed record SourceKnownRoutingRequest(string Label);

public sealed class SourceKnownRoutingDto(SourceKnownEntity? entity = null) : Dto(entity)
{
    public string Label { get; init; } = string.Empty;
}

public sealed class SourceKnownRoutingResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public string Label { get; init; } = string.Empty;
}

public interface ISourceKnownRoutingService
    : ISourceKnownService<SourceKnownRoutingEntity, SourceKnownRoutingResponse, SourceKnownRoutingRequest>;

public sealed class SourceKnownRoutingService(ISourceKnownRepository<SourceKnownRoutingEntity> repository)
    : SourceKnownService<SourceKnownRoutingEntity, SourceKnownRoutingResponse, SourceKnownRoutingRequest>(repository),
        ISourceKnownRoutingService
{
    protected override Task<SourceKnownRoutingResponse> CreateResponseCoreAsync(
        SourceKnownRoutingEntity entity, CancellationToken cancellationToken = default)
        => Task.FromResult(new SourceKnownRoutingResponse
        {
            Id = entity.EntityId,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            Label = entity.Label
        });

    protected override Task<SourceKnownRoutingEntity> CreateEntityCoreAsync(
        SourceKnownRoutingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SourceKnownRoutingEntity { Label = request.Label });
}

[Route("source-known-routing")]
[Authorize(Policy = SourceKnownRoutingPolicies.Read)]
public sealed class SourceKnownRoutingController(ISourceKnownRoutingService service)
    : SourceKnownController<SourceKnownRoutingEntity, SourceKnownRoutingResponse, SourceKnownRoutingRequest>(service)
{
    protected override Task<SourceKnownRoutingEntity> CreateEntityAsync(SourceKnownRoutingRequest request, CancellationToken cancellationToken)
        => throw ExceptionFor.Forbidden("Routing fixture does not allow writes.");

    [Authorize(Policy = SourceKnownRoutingPolicies.Write)]
    public override Task<ActionResult<SourceKnownRoutingResponse>> PostAsync(SourceKnownRoutingRequest request, CancellationToken cancellationToken = default)
        => base.PostAsync(request, cancellationToken);

    [NonAction]
    public override Task<long> CountAsync(CancellationToken cancellationToken = default) => base.CountAsync(cancellationToken);

    [NonAction]
    public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => base.DeleteAsync(id, cancellationToken);

    [ApiExplorerSettings(IgnoreApi = true)]
    public override Task<bool> AnyAsync(CancellationToken cancellationToken = default) => base.AnyAsync(cancellationToken);
}

[Route("source-known-writes")]
[Authorize(Policy = SourceKnownRoutingPolicies.Read)]
public sealed class SourceKnownWritableRoutingController(ISourceKnownRoutingService service)
    : SourceKnownController<SourceKnownRoutingEntity, SourceKnownRoutingResponse, SourceKnownRoutingRequest>(service)
{
    [Authorize(Policy = SourceKnownRoutingPolicies.Write)]
    public override Task<ActionResult<SourceKnownRoutingResponse>> PostAsync(SourceKnownRoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Label == "denied")
            throw ExceptionFor.Forbidden("Creation denied for this resource.");

        return base.PostAsync(request, cancellationToken);
    }

    [Authorize(Policy = SourceKnownRoutingPolicies.Write)]
    public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty)
            throw ExceptionFor.Forbidden("Deletion denied for this resource.");

        return base.DeleteAsync(id, cancellationToken);
    }
}
