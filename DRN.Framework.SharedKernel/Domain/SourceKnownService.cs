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
