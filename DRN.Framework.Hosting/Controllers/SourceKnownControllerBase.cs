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

    /// <summary>Projects an entity to its public response representation through the backing service.</summary>
    protected async Task<TResponse> CreateResponseAsync(TEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return await service.CreateResponseAsync(entity, cancellationToken)
               ?? throw new InvalidOperationException($"{service.GetType().Name}.{nameof(CreateResponseAsync)} returned null.");
    }

    /// <summary>Projects a batch of entities to their public response representations through the backing service.</summary>
    protected async Task<TResponse[]> CreateResponsesAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entities);
        var responses = await service.CreateResponsesAsync(entities, cancellationToken)
                        ?? throw new InvalidOperationException($"{service.GetType().Name}.{nameof(CreateResponsesAsync)} returned null.");
        if (responses.Length != entities.Count)
            throw new InvalidOperationException(
                $"{service.GetType().Name}.{nameof(CreateResponsesAsync)} returned {responses.Length} responses for {entities.Count} entities.");
        foreach (var response in responses)
        {
            if (response is null)
                throw new InvalidOperationException($"{service.GetType().Name}.{nameof(CreateResponsesAsync)} returned a null element.");
        }

        return responses;
    }

    /// <summary>Links request cancellation to the scoped repository before starting work.</summary>
    protected void PrepareRepository(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Repository.CancelWhen(cancellationToken);
    }
}
