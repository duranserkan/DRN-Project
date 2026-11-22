using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Exposes only create and delete HTTP actions. Inherited reads remain callable from C#,
/// but sealed <see cref="NonActionAttribute"/> overrides exclude them from MVC routing.
/// </summary>
[SuppressMessage("SonarQube", "S2436", Justification = "The write-only variant preserves the base controller's independent entity, response contract and create request contracts while excluding read actions from MVC routing.")]
public abstract class SourceKnownWriteOnlyController<TEntity, TResponse, TCreateRequest>(
    ISourceKnownService<TEntity, TResponse, TCreateRequest> service)
    : SourceKnownController<TEntity, TResponse, TCreateRequest>(service)
    where TEntity : AggregateRoot
    where TResponse : class
    where TCreateRequest : class
{
    /// <inheritdoc />
    [NonAction]
    public sealed override Task<TResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => base.GetAsync(id, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<TResponse[]> GetManyAsync(Guid[] ids, CancellationToken cancellationToken = default)
        => base.GetManyAsync(ids, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => base.AnyAsync(cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<long> CountAsync(CancellationToken cancellationToken = default)
        => base.CountAsync(cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<PaginationResultModel<TResponse>> PaginateAsync(
        PaginationRequest? request = null, CancellationToken cancellationToken = default)
        => base.PaginateAsync(request, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<PaginationResultModel<TResponse>> PaginateWithOptionsAsync(
        PaginationOptions? options = null, CancellationToken cancellationToken = default)
        => base.PaginateWithOptionsAsync(options, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<PaginationResultModel<TResponse>> PaginateWithBodyAsync(
        PaginationRequest request, CancellationToken cancellationToken = default)
        => base.PaginateWithBodyAsync(request, cancellationToken);
}
