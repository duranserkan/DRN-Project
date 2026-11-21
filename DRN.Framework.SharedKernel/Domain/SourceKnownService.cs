using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Domain.Repository;

namespace DRN.Framework.SharedKernel.Domain;

/// <summary>
/// Provides a base service backed by a repository for entities of type <typeparamref name="TEntity"/>.
/// </summary>
public abstract class SourceKnownService<TEntity>(ISourceKnownRepository<TEntity> repository) : ISourceKnownService<TEntity>
    where TEntity : AggregateRoot
{
    /// <inheritdoc />
    public ISourceKnownRepository<TEntity> Repository { get; } = repository;
}

/// <summary>
/// Provides a base repository-backed service for entities of type <typeparamref name="TEntity"/>
/// with default DTO projection delegating to <see cref="SourceKnownEntity.ToDto()"/>.
/// </summary>
public abstract class SourceKnownService<TEntity, TDto>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownService<TEntity>(repository), ISourceKnownService<TEntity, TDto>
    where TEntity : AggregateRoot
    where TDto : Dto
{
    /// <inheritdoc />
    public virtual TDto ToDto(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var mapped = entity.ToDto();
        return mapped is TDto dto
            ? dto
            : throw new InvalidOperationException(
                $"{entity.GetType().Name}.ToDto() returned '{mapped?.GetType().Name ?? "null"}', which is not assignable to '{typeof(TDto).Name}'.");
    }
}

/// <summary>
/// Provides a base repository-backed service for entities of type <typeparamref name="TEntity"/>
/// with DTO projection to <typeparamref name="TDto"/> and entity creation from <typeparamref name="TCreateRequest"/>.
/// </summary>
[SuppressMessage("SonarQube", "S2436", Justification = "Entity, DTO projection and create request are independent contracts. Three type parameters preserve typed repository access, DTO projection and request handling.")]
public abstract class SourceKnownService<TEntity, TDto, TCreateRequest>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownService<TEntity, TDto>(repository), ISourceKnownService<TEntity, TDto, TCreateRequest>
    where TEntity : AggregateRoot
    where TDto : Dto
    where TCreateRequest : class
{
    /// <inheritdoc />
    public abstract Task<TEntity> CreateEntityAsync(TCreateRequest request, CancellationToken cancellationToken = default);
}
