using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.Utils.Entity;

//todo add async enumerable support
public interface IPaginationUtils
{
    Task<PaginationResult<TEntity>> ToPaginationResultAsync<TEntity>(
        IQueryable<TEntity> query,
        PaginationRequest request, CancellationToken? cancellationToken = null)
        where TEntity : SourceKnownEntity;
}

[Singleton<IPaginationUtils>]
public class PaginationUtils(ISourceKnownEntityIdUtils utils) : IPaginationUtils
{
    public async Task<PaginationResult<TEntity>> ToPaginationResultAsync<TEntity>(
        IQueryable<TEntity> query,
        PaginationRequest request, CancellationToken? cancellationToken = null)
        where TEntity : SourceKnownEntity
    {
        var ct = cancellationToken ?? CancellationToken.None;
        var totalCount = -1L;

        if (request.UpdateTotalCount)
            totalCount = await query.LongCountAsync(ct);

        var sortDirection = request.PageCursor.SortDirection;
        var navigationDirection = request.NavigationDirection;
        var filteredQuery = query;
        if (!request.PageCursor.IsFirstRequest)
        {
            //Valid SourceKnownIds carry created date information which is monotonic with drift protection
            //For this reason it can be used for sorting and filtering
            var firstEntityId = utils.Parse(request.PageCursor.FirstId);
            var lastEntityId = utils.Parse(request.PageCursor.LastId);

            if (!firstEntityId.Valid)
                throw new ValidationException($"Invalid PaginationRequest.PageCursor.FirstId: {request.PageCursor.LastId}");
            if (!lastEntityId.Valid)
                throw new ValidationException($"Invalid PaginationRequest.PageCursor.LastId: {request.PageCursor.LastId}");

            if (navigationDirection == NavigationDirection.Same)
            {
                var firstId = firstEntityId.Source.Id;
                var lastId = lastEntityId.Source.Id;
                filteredQuery = sortDirection == PageSortDirection.AscendingByCreatedAt
                    ? query.Where(entity => entity.Id >= firstId && entity.Id <= lastId)
                    : query.Where(entity => entity.Id >= lastId && entity.Id <= firstId);
            }
            else if (navigationDirection == NavigationDirection.Next)
            {
                var cursorId = lastEntityId.Source.Id;
                filteredQuery = sortDirection == PageSortDirection.AscendingByCreatedAt
                    ? query.Where(entity => entity.Id > cursorId)
                    : query.Where(entity => entity.Id < cursorId);
            }
            else //previous page
            {
                var cursorId = firstEntityId.Source.Id;
                filteredQuery = sortDirection == PageSortDirection.AscendingByCreatedAt
                    ? query.Where(entity => entity.Id < cursorId)
                    : query.Where(entity => entity.Id > cursorId);
            }
        }

        IOrderedQueryable<TEntity> orderedQuery;
        if (navigationDirection == NavigationDirection.Previous)
        {
            orderedQuery = sortDirection == PageSortDirection.DescendingByCreatedAt
                ? filteredQuery.OrderBy(entity => entity.Id)
                : filteredQuery.OrderByDescending(entity => entity.Id);
        }
        else
        {
            orderedQuery = sortDirection == PageSortDirection.AscendingByCreatedAt
                ? filteredQuery.OrderBy(entity => entity.Id)
                : filteredQuery.OrderByDescending(entity => entity.Id);
        }

        IReadOnlyList<TEntity> items = request.IsPageJump()
            ? await orderedQuery.Skip(request.GetSkipSize()).Take(request.PageSize.Size + 1).ToListAsync(ct)
            : await orderedQuery.Take(request.PageSize.Size + 1).ToListAsync(ct);
        
        if (navigationDirection == NavigationDirection.Previous)
        {
            items = sortDirection == PageSortDirection.AscendingByCreatedAt
                ? items.OrderBy(entity => entity.Id).ToArray()
                : items.OrderByDescending(entity => entity.Id).ToArray();
        }

        return new PaginationResult<TEntity>(items, request, totalCount);
    }
}