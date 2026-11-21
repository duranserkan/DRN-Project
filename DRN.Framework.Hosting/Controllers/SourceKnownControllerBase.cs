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
public abstract class SourceKnownControllerBase<TEntity, TResponse>
    : ControllerBase
    where TEntity : AggregateRoot
    where TResponse : class
{
    /// <summary>Initializes the controller with its backing application service.</summary>
    protected SourceKnownControllerBase(ISourceKnownService<TEntity> service)
    {
        if (typeof(Dto).IsAssignableFrom(typeof(TResponse)))
            throw new InvalidOperationException(
                $"{nameof(SourceKnownControllerBase<TEntity, TResponse>)} response type '{typeof(TResponse).Name}' cannot derive from {nameof(Dto)}. Use an explicit Contract response model.");

        ArgumentNullException.ThrowIfNull(service);
        Service = service;
    }

    /// <summary>Application service backing this controller.</summary>
    protected ISourceKnownService<TEntity> Service { get; }

    /// <summary>Repository exposed through <see cref="Service"/>.</summary>
    public ISourceKnownRepository<TEntity> Repository => Service.Repository;

    /// <summary>Projects an entity to its public response representation.</summary>
    protected abstract TResponse ToResponse(TEntity entity);

    /// <summary>Links request cancellation to the scoped repository before starting work.</summary>
    protected void PrepareRepository(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Repository.CancelWhen(cancellationToken);
    }
}
