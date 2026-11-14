using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Exposes only create and delete HTTP actions. Inherited reads remain callable from C#,
/// but sealed <see cref="NonActionAttribute"/> overrides exclude them from MVC routing.
/// </summary>
public abstract class SourceKnownWriteOnlyController<TEntity, TDto, TCreateRequest>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownController<TEntity, TDto, TCreateRequest>(repository)
    where TEntity : AggregateRoot
    where TDto : Dto
    where TCreateRequest : class
{
    /// <inheritdoc />
    [NonAction]
    public sealed override Task<TDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => base.GetAsync(id, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<TDto[]> GetManyAsync(Guid[] ids, CancellationToken cancellationToken = default)
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
    public sealed override Task<PaginationResultModel<TDto>> PaginateAsync(
        PaginationRequest? request = null, CancellationToken cancellationToken = default)
        => base.PaginateAsync(request, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<PaginationResultModel<TDto>> PaginateWithOptionsAsync(
        PaginationOptions options, CancellationToken cancellationToken = default)
        => base.PaginateWithOptionsAsync(options, cancellationToken);

    /// <inheritdoc />
    [NonAction]
    public sealed override Task<PaginationResultModel<TDto>> PaginateWithBodyAsync(
        PaginationRequest request, CancellationToken cancellationToken = default)
        => base.PaginateWithBodyAsync(request, cancellationToken);
}
