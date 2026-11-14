using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.SharedKernel.Domain.Repository;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Framework.Hosting.Controllers;

/// <summary>
/// Exposes bounded repository reads as DTOs. Override an action with <see cref="NonActionAttribute"/>
/// to remove it from routing. Repository filters must enforce application data visibility.
/// </summary>
public abstract class SourceKnownReadOnlyController<TEntity, TDto>(ISourceKnownRepository<TEntity> repository)
    : SourceKnownControllerBase<TEntity, TDto>(repository)
    where TEntity : AggregateRoot
    where TDto : Dto
{
    /// <summary>Server-owned page ceiling, bounded to 1 through 1000.</summary>
    protected virtual int MaxPageSize => PageSize.MaxSizeDefault;

    /// <summary>Server-owned batch ceiling, bounded to 1 through 1000.</summary>
    protected virtual int MaxBatchSize => PageSize.MaxSizeDefault;

    /// <summary>Retrieves one entity using the repository's configured GUID format.</summary>
    [HttpGet("{id:guid}")]
    public virtual async Task<TDto> GetAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        PrepareRepository(cancellationToken);
        return ToDto(await Repository.GetAsync(id));
    }

    /// <summary>Retrieves a bounded batch. Missing IDs and ordering follow repository semantics.</summary>
    [HttpPost("Lookup")]
    public virtual async Task<TDto[]> GetManyAsync([FromBody] Guid[] ids, CancellationToken cancellationToken = default)
    {
        if (ids is null || ids.Length == 0 || ids.Length > Math.Clamp(MaxBatchSize, 1, PageSize.MaxSizeThreshold))
            throw ExceptionFor.Validation("Provide a nonempty ID batch within the server's batch limit.");

        PrepareRepository(cancellationToken);
        var entities = await Repository.GetAsync(ids);
        return entities.Select(ToDto).ToArray();
    }

    /// <summary>Reports whether any entity is visible through the configured repository filters.</summary>
    [HttpGet("Any")]
    public virtual Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        PrepareRepository(cancellationToken);
        return Repository.AnyAsync();
    }

    /// <summary>Counts entities visible through the configured repository filters.</summary>
    [HttpGet("Count")]
    public virtual Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        PrepareRepository(cancellationToken);
        return Repository.CountAsync();
    }

    /// <summary>Fetches a bounded page using query-bound cursor metadata.</summary>
    [HttpGet]
    public virtual Task<PaginationResultModel<TDto>> PaginateAsync(
        [FromQuery] PaginationRequest? request = null, CancellationToken cancellationToken = default)
        => ReadPageAsync(request ?? PaginationRequest.Default, cancellationToken);

    /// <summary>Fetches the first page using flat query options and the server's page ceiling.</summary>
    [HttpGet("Paginate")]
    public virtual Task<PaginationResultModel<TDto>> PaginateWithOptionsAsync(
        [FromQuery] PaginationOptions options, CancellationToken cancellationToken = default)
        => ReadPageAsync(PaginationRequest.DefaultWith(options), cancellationToken);

    /// <summary>Fetches a bounded page using JSON cursor metadata. This action does not mutate data.</summary>
    [HttpPost("Paginate")]
    public virtual Task<PaginationResultModel<TDto>> PaginateWithBodyAsync(
        [FromBody] PaginationRequest request, CancellationToken cancellationToken = default)
        => ReadPageAsync(request, cancellationToken);

    private async Task<PaginationResultModel<TDto>> ReadPageAsync(PaginationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        PrepareRepository(cancellationToken);

        var cursor = request.PageCursor;
        var maxPage = cursor.PageNumber > long.MaxValue - 10 ? long.MaxValue : cursor.PageNumber + 10;
        var pageNumber = cursor.IsFirstRequest
            ? 1
            : Math.Clamp(request.PageNumber, Math.Max(1, cursor.PageNumber - 10), maxPage);

        // The server owns limits and counts, even when clients return previous page metadata.
        var boundedRequest = new PaginationRequest(pageNumber,
            new PageSize(request.PageSize.Size, Math.Clamp(MaxPageSize, 1, PageSize.MaxSizeThreshold)),
            cursor, updateTotalCount: request.UpdateTotalCount);
        var result = await Repository.PaginateAsync(boundedRequest);
        return result.ToModel(ToDto);
    }
}
