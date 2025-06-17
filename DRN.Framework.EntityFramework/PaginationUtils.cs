using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework;

public interface IPaginationUtils
{
    Task<PaginationResult<TEntity>> ToPaginationResultAsync<TEntity>(
        IQueryable<TEntity> query,
        PaginationRequest request, CancellationToken? ct = null)
        where TEntity : SourceKnownEntity;
}

[Transient<IPaginationUtils>]
public class PaginationUtils(ISourceKnownEntityIdUtils utils) : IPaginationUtils
{
    public async Task<PaginationResult<TEntity>> ToPaginationResultAsync<TEntity>(
        IQueryable<TEntity> query,
        PaginationRequest request, CancellationToken? cancellationToken = null)
        where TEntity : SourceKnownEntity
    {
        // Task<long>? countTask = null;
        // if (request.UpdateTotalCount) 
        //     countTask = source.LongCountAsync(cancellationToken);

        var ct = cancellationToken ?? CancellationToken.None;
        var totalCount = -1L;
        
        if (request.UpdateTotalCount)
            totalCount = await query.LongCountAsync(ct); //todo parallelize

        var filteredQuery = query;
        if (!request.PageCursor.IsFirstRequest)
        {
            //SourceKnownId carries created date information which is monotonic with drift protection
            var sourceKnownEntityId = utils.Parse(request.PageCursor.LastId);
            if (sourceKnownEntityId.Valid)
                throw new ValidationException($"Invalid PaginationRequest.PageCursor.LastId: {request.PageCursor.LastId}");

            filteredQuery = request.PageCursor.SortDirection == PageSortDirection.AscendingByCreatedAt
                ? query.Where(x => x.Id > sourceKnownEntityId.Source.Id)
                : query.Where(x => x.Id < sourceKnownEntityId.Source.Id);
        }

        var orderedQuery = request.PageCursor.SortDirection == PageSortDirection.AscendingByCreatedAt
            ? filteredQuery.OrderBy(x => x.Id)
            : filteredQuery.OrderByDescending(x => x.Id);

        var items = await orderedQuery
            .Take(request.PageSize.Size)
            .ToListAsync(ct);

        return new PaginationResult<TEntity>(items, request, totalCount);
    }
}