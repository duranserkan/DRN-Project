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
    public abstract Task<TResponse> CreateResponseAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public virtual async Task<TResponse[]> CreateResponsesAsync(IReadOnlyList<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        cancellationToken.ThrowIfCancellationRequested();
        if (entities.Count == 0)
            return [];

        var responses = new TResponse[entities.Count];
        for (var i = 0; i < entities.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = entities[i];
            ArgumentNullException.ThrowIfNull(entity);
            responses[i] = await CreateResponseAsync(entity, cancellationToken)
                           ?? throw new InvalidOperationException($"{GetType().Name}.{nameof(CreateResponseAsync)} returned null.");
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
    public abstract Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken = default);
}
