using DRN.Framework.Hosting.Controllers;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Test.Utils.Hosting;

public static class SourceKnownRoutingPolicies
{
    public const string Read = "SourceKnown.Read";
    public const string Write = "SourceKnown.Write";
    public const string WriteClaim = "source-known-write";
}

[TestEntityType(249)]
public sealed class SourceKnownRoutingEntity : AggregateRoot
{
    public string Label { get; init; } = string.Empty;
}

public sealed record SourceKnownRoutingRequest(string Label);

public sealed class SourceKnownRoutingDto : Dto
{
    public string Label { get; init; } = string.Empty;
}

[Route("source-known-routing")]
[Authorize(Policy = SourceKnownRoutingPolicies.Read)]
public sealed class SourceKnownRoutingController(ISourceKnownRepository<SourceKnownRoutingEntity> repository)
    : SourceKnownController<SourceKnownRoutingEntity, Dto, SourceKnownRoutingRequest>(repository)
{
    protected override Dto ToDto(SourceKnownRoutingEntity entity) => new();

    protected override Task<SourceKnownRoutingEntity> CreateEntityAsync(SourceKnownRoutingRequest request, CancellationToken cancellationToken)
        => throw ExceptionFor.Forbidden("Routing fixture does not allow writes.");

    [Authorize(Policy = SourceKnownRoutingPolicies.Write)]
    public override Task<ActionResult<Dto>> PostAsync(SourceKnownRoutingRequest request, CancellationToken cancellationToken = default)
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
public sealed class SourceKnownWritableRoutingController(ISourceKnownRepository<SourceKnownRoutingEntity> repository)
    : SourceKnownController<SourceKnownRoutingEntity, SourceKnownRoutingDto, SourceKnownRoutingRequest>(repository)
{
    protected override SourceKnownRoutingDto ToDto(SourceKnownRoutingEntity entity) => new() { Label = entity.Label };

    [Authorize(Policy = SourceKnownRoutingPolicies.Write)]
    public override Task<ActionResult<SourceKnownRoutingDto>> PostAsync(SourceKnownRoutingRequest request,
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

    protected override Task<SourceKnownRoutingEntity> CreateEntityAsync(SourceKnownRoutingRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new SourceKnownRoutingEntity { Label = request.Label });
}
