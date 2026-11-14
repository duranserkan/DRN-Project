using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Provides repository access and DTO mapping without exposing HTTP actions.
/// Concrete controllers own routing, named authorization policies and resource authorization.
/// </summary>
[ApiController]
[Authorize]
public abstract class SourceKnownControllerBase<TEntity, TDto>(ISourceKnownRepository<TEntity> repository)
    : ControllerBase, ISourceKnownService<TEntity>
    where TEntity : AggregateRoot
    where TDto : Dto
{
    /// <inheritdoc />
    public ISourceKnownRepository<TEntity> Repository { get; } = repository;

    /// <summary>Maps an entity to its public representation.</summary>
    protected abstract TDto ToDto(TEntity entity);

    /// <summary>Links request cancellation to the scoped repository before starting work.</summary>
    protected void PrepareRepository(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Repository.CancelWhen(cancellationToken);
    }
}
