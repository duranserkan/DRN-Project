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
/// that projects entities to <typeparamref name="TDto"/>.
/// </summary>
public interface ISourceKnownService<TEntity, out TDto> : ISourceKnownService<TEntity>
    where TEntity : AggregateRoot
    where TDto : Dto
{
    /// <summary>
    /// Projects <paramref name="entity"/> to its <typeparamref name="TDto"/> representation.
    /// </summary>
    TDto ToDto(TEntity entity);
}

/// <summary>
/// Represents a repository-backed service for entities of type <typeparamref name="TEntity"/>
/// with DTO projection to <typeparamref name="TDto"/> and entity creation from <typeparamref name="TCreateRequest"/>.
/// </summary>
[SuppressMessage("SonarQube", "S2436", Justification = "Entity, DTO projection and create request are independent contracts. Three type parameters preserve covariant DTO output and contravariant request input.")]
public interface ISourceKnownService<TEntity, out TDto, in TCreateRequest> : ISourceKnownService<TEntity, TDto>
    where TEntity : AggregateRoot
    where TDto : Dto
    where TCreateRequest : class
{
    /// <summary>
    /// Builds a new <typeparamref name="TEntity"/> from <paramref name="request"/> through application domain operations without saving it.
    /// </summary>
    Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken = default);
}
