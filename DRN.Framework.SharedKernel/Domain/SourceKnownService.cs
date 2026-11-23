using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Domain.Repository;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Provides a base service backed by a repository for entities of type <typeparamref name="TEntity"/>.
/// </summary>
public abstract class SourceKnownService<TEntity> : ISourceKnownService<TEntity>
    where TEntity : AggregateRoot
{
    /// <summary>Initializes the service with its backing repository.</summary>
    protected SourceKnownService(ISourceKnownRepository<TEntity> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        Repository = repository;
    }

    /// <inheritdoc />
    public ISourceKnownRepository<TEntity> Repository { get; }
}

/// <summary>
/// Provides a base repository-backed service for entities of type <typeparamref name="TEntity"/>
/// that projects entities to explicit <typeparamref name="TResponse"/> contracts.
/// </summary>
public abstract class SourceKnownService<TEntity, TResponse>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownService<TEntity>(repository), ISourceKnownService<TEntity, TResponse>
    where TEntity : AggregateRoot
    where TResponse : class
{
    /// <inheritdoc />
    public async Task<TResponse> CreateResponseAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateResponseCoreAsync(entity, cancellationToken)
               ?? throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateResponseCoreAsync)} returned null.");
    }

    /// <inheritdoc />
    public async Task<TResponse[]> CreateResponsesAsync(IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        cancellationToken.ThrowIfCancellationRequested();
        if (entities.Count == 0)
            return [];

        foreach (var t in entities)
            ArgumentNullException.ThrowIfNull(t, nameof(entities));

        var responses = await CreateResponsesCoreAsync(entities, cancellationToken)
                        ?? throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateResponsesCoreAsync)} returned null.");

        if (responses.Length != entities.Count)
            throw new InvalidOperationException(
                $"{GetType().Name}.{nameof(CreateResponsesCoreAsync)} returned {responses.Length} responses for {entities.Count} entities.");

        foreach (var response in responses)
            if (response is null)
                throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateResponsesCoreAsync)} returned a null element.");

        return responses;
    }

    /// <summary>
    /// Projects a validated non-null <paramref name="entity"/> to its explicit <typeparamref name="TResponse"/> contract.
    /// </summary>
    protected abstract Task<TResponse> CreateResponseCoreAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a validated nonempty list of non-null <paramref name="entities"/> to explicit <typeparamref name="TResponse"/> contracts in order.
    /// Override to batch asynchronous enrichment queries for collections and paginated results.
    /// </summary>
    protected virtual async Task<TResponse[]> CreateResponsesCoreAsync(IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        var responses = new TResponse[entities.Count];
        for (var i = 0; i < entities.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            responses[i] = await CreateResponseCoreAsync(entities[i], cancellationToken)
                           ?? throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateResponseCoreAsync)} returned null.");
        }

        return responses;
    }
}

/// <summary>
/// Provides a base repository-backed service for entities of type <typeparamref name="TEntity"/>
/// with response projection to <typeparamref name="TResponse"/> and entity creation from <typeparamref name="TCreateRequest"/>.
/// </summary>
[SuppressMessage("SonarQube", "S2436", Justification = "Entity, response contract and create request are independent contracts. Three type parameters preserve typed repository access, response projection and request handling.")]
public abstract class SourceKnownService<TEntity, TResponse, TCreateRequest>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownService<TEntity, TResponse>(repository), ISourceKnownService<TEntity, TResponse, TCreateRequest>
    where TEntity : AggregateRoot
    where TResponse : class
    where TCreateRequest : class
{
    /// <inheritdoc />
    public async Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return await CreateEntityCoreAsync(request, cancellationToken)
               ?? throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateEntityCoreAsync)} returned null.");
    }

    /// <summary>
    /// Builds a new <typeparamref name="TEntity"/> from a validated non-null <paramref name="request"/> through application domain operations without saving it.
    /// </summary>
    protected abstract Task<TEntity> CreateEntityCoreAsync(TCreateRequest request, CancellationToken cancellationToken = default);
}
