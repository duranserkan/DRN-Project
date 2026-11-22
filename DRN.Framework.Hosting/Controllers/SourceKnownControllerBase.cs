using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Provides service and repository access plus explicit response mapping without exposing HTTP actions.
/// Concrete controllers own routing, named authorization policies and resource authorization.
/// </summary>
[ApiController]
[Authorize]
public abstract class SourceKnownControllerBase<TEntity, TResponse>(ISourceKnownService<TEntity, TResponse> service)
    : ControllerBase
    where TEntity : AggregateRoot
    where TResponse : class
{
    /// <summary>Repository exposed through the backing service.</summary>
    protected ISourceKnownRepository<TEntity> Repository => service.Repository;

    /// <summary>
    /// Projects an entity to its public response representation through the backing service.
    /// When overriding on a controller that exposes collection or pagination actions, also override <see cref="CreateResponsesAsync"/>.
    /// </summary>
    protected virtual Task<TResponse> CreateResponseAsync(TEntity entity, CancellationToken cancellationToken)
        => service.CreateResponseAsync(entity, cancellationToken);

    /// <summary>Projects a batch of entities to their public response representations through the backing service.</summary>
    protected virtual Task<TResponse[]> CreateResponsesAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
        => service.CreateResponsesAsync(entities, cancellationToken);

    /// <summary>Links request cancellation to the scoped repository before starting work.</summary>
    protected void PrepareRepository(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Repository.CancelWhen(cancellationToken);
    }
}
