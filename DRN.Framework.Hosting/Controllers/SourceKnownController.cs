using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Combines all read actions with opt-in create and delete actions.
/// Applications supply request mapping, explicit authorization policies and resource-specific checks.
/// </summary>
public abstract class SourceKnownController<TEntity, TDto, TCreateRequest>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownReadOnlyController<TEntity, TDto>(repository)
    where TEntity : AggregateRoot
    where TDto : Dto
    where TCreateRequest : class
{
    /// <summary>Builds an entity through application domain operations.</summary>
    protected abstract Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken);

    /// <summary>Creates an entity and returns its DTO. Does not require the GET action to be enabled.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public virtual async Task<ActionResult<TDto>> PostAsync([FromBody] TCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PrepareRepository(cancellationToken);
        var entity = await CreateEntityAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await Repository.CreateAsync(entity);

        return new ObjectResult(ToDto(entity)) { StatusCode = StatusCodes.Status201Created };
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
