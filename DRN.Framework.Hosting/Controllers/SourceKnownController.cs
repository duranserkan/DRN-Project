using DRN.Framework.SharedKernel.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Combines all read actions with opt-in create and delete actions backed by an application service.
/// Applications supply explicit response projection, named authorization policies and resource-specific checks.
/// </summary>
public abstract class SourceKnownController<TEntity, TResponse, TCreateRequest>
    : SourceKnownReadOnlyController<TEntity, TResponse>
    where TEntity : AggregateRoot
    where TResponse : class
    where TCreateRequest : class
{
    /// <summary>Initializes the controller with a service accepting an explicit HTTP request.</summary>
    protected SourceKnownController(ISourceKnownService<TEntity, TResponse, TCreateRequest> service) : base(service)
    {
        Service = service;
    }

    /// <summary>Gets the backing application service with read, response projection and entity creation support.</summary>
    protected new ISourceKnownService<TEntity, TResponse, TCreateRequest> Service { get; }

    /// <summary>Builds an entity through the backing application service.</summary>
    protected virtual Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken)
        => Service.CreateEntityAsync(request, cancellationToken);

    /// <summary>Creates an entity and returns its response contract. Does not require the GET action to be enabled.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public virtual async Task<ActionResult<TResponse>> PostAsync([FromBody] TCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PrepareRepository(cancellationToken);
        var entity = await CreateEntityAsync(request, cancellationToken)
                     ?? throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateEntityAsync)} returned null.");
        cancellationToken.ThrowIfCancellationRequested();
        await Repository.CreateAsync(entity);
        cancellationToken.ThrowIfCancellationRequested();
        var response = await Service.CreateResponseAsync(entity, cancellationToken);

        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>
    /// Deletes by GUID using repository filters and direct-delete semantics, including for missing records.
    /// Does not invoke entity deletion lifecycle hooks.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public virtual async Task<NoContentResult> DeleteAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        PrepareRepository(cancellationToken);
        await Repository.DeleteAsync(id);
        return NoContent();
    }
}
