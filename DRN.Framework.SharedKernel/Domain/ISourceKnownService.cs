using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Domain.Repository;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Represents a service backed by a repository for entities of type <typeparamref name="TEntity"/>.
/// </summary>
public interface ISourceKnownService<TEntity>
    where TEntity : AggregateRoot
{
    /// <summary>
    /// Gets the repository used by this service.
    /// </summary>
    ISourceKnownRepository<TEntity> Repository { get; }
}

/// <summary>
/// Represents a repository-backed service for entities of type <typeparamref name="TEntity"/>
/// that projects entities to explicit <typeparamref name="TResponse"/> contracts.
/// </summary>
public interface ISourceKnownService<TEntity, TResponse> : ISourceKnownService<TEntity>
    where TEntity : AggregateRoot
    where TResponse : class
{
    /// <summary>
    /// Projects <paramref name="entity"/> to its explicit <typeparamref name="TResponse"/> contract.
    /// </summary>
    Task<TResponse> CreateResponseAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects <paramref name="entities"/> to explicit <typeparamref name="TResponse"/> contracts, returning one non-null response per input entity in order.
    /// Override to batch asynchronous enrichment queries for collections and paginated results.
    /// </summary>
    Task<TResponse[]> CreateResponsesAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a repository-backed service for entities of type <typeparamref name="TEntity"/>
/// with response projection to <typeparamref name="TResponse"/> and entity creation from <typeparamref name="TCreateRequest"/>.
/// </summary>
[SuppressMessage("SonarQube", "S2436", Justification = "Entity, response contract and create request are independent contracts. Three type parameters preserve typed repository access, response projection and contravariant request input.")]
public interface ISourceKnownService<TEntity, TResponse, in TCreateRequest> : ISourceKnownService<TEntity, TResponse>
    where TEntity : AggregateRoot
    where TResponse : class
    where TCreateRequest : class
{
    /// <summary>
    /// Builds a new <typeparamref name="TEntity"/> from <paramref name="request"/> through application domain operations without saving it.
    /// </summary>
    Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken = default);
}
