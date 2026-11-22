using DRN.Framework.SharedKernel.Domain;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Combines all read actions with opt-in create and delete actions backed by an application service.
/// Applications supply explicit response projection, named authorization policies and resource-specific checks.
/// </summary>
[SuppressMessage("SonarQube", "S2436", Justification = "Entity, response contract and create request are independent contracts. Three type parameters preserve typed service access, response mapping and request binding.")]
public abstract class SourceKnownController<TEntity, TResponse, TCreateRequest>(
    ISourceKnownService<TEntity, Dto, TCreateRequest> service)
    : SourceKnownReadOnlyController<TEntity, TResponse>(service)
    where TEntity : AggregateRoot
    where TResponse : class
    where TCreateRequest : class
{
    /// <summary>Builds an entity through the backing application service.</summary>
    protected virtual Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken)
        => ((ISourceKnownService<TEntity, Dto, TCreateRequest>)Service).CreateEntityAsync(request, cancellationToken);

    /// <summary>Creates an entity and returns its response contract. Does not require the GET action to be enabled.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public virtual async Task<ActionResult<TResponse>> PostAsync([FromBody] TCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PrepareRepository(cancellationToken);
        var entity = await CreateEntityAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await Repository.CreateAsync(entity);

        return new ObjectResult(ToResponse(entity)) { StatusCode = StatusCodes.Status201Created };
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
