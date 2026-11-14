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
