using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Ids;
using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.Utils.Entity;

public interface IPaginationUtils
{
    Task<PaginationResult<TEntity>> GetResultAsync<TEntity>(IQueryable<TEntity> query, PaginationRequest request,
        CancellationToken? cancellationToken = null) where TEntity : SourceKnownEntity;
}

[Singleton<IPaginationUtils>]
public class PaginationUtils(ISourceKnownEntityIdUtils utils) : IPaginationUtils
{
    public async Task<PaginationResult<TEntity>> GetResultAsync<TEntity>(IQueryable<TEntity> query, PaginationRequest request,
        CancellationToken? cancellationToken = null) where TEntity : SourceKnownEntity
    {
        var ct = cancellationToken ?? CancellationToken.None;
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

            filteredQuery = FilterQueryByCursor(query, firstEntityId, lastEntityId, navigationDirection, sortDirection);
        }

        var orderedQuery = SortByEntityId(filteredQuery, navigationDirection, sortDirection);
        var items = await GetEntitiesAsync(orderedQuery, request, navigationDirection, sortDirection, ct);

        var totalCount = request.Total.Count != -1 ? request.Total.Count : -1;
        if (request.UpdateTotalCount)
            totalCount = await query.LongCountAsync(ct);

        return new PaginationResult<TEntity>(items, request, totalCount);
    }

    private static IQueryable<TEntity> FilterQueryByCursor<TEntity>(IQueryable<TEntity> query, SourceKnownEntityId firstEntityId, SourceKnownEntityId lastEntityId,
        NavigationDirection navigation, PageSortDirection sort) where TEntity : SourceKnownEntity
    {
        IQueryable<TEntity> filteredQuery;
        if (navigation == NavigationDirection.Refresh)
        {
            var firstId = firstEntityId.Source.Id;
            var lastId = lastEntityId.Source.Id;
            filteredQuery = sort == PageSortDirection.AscendingByCreatedAt
                ? query.Where(entity => entity.Id >= firstId && entity.Id <= lastId)
                : query.Where(entity => entity.Id >= lastId && entity.Id <= firstId);
        }
        else if (navigation == NavigationDirection.Next)
        {
            var cursorId = lastEntityId.Source.Id;
            filteredQuery = sort == PageSortDirection.AscendingByCreatedAt
                ? query.Where(entity => entity.Id > cursorId)
                : query.Where(entity => entity.Id < cursorId);
        }
        else //previous page
        {
            var cursorId = firstEntityId.Source.Id;
            filteredQuery = sort == PageSortDirection.AscendingByCreatedAt
                ? query.Where(entity => entity.Id < cursorId)
                : query.Where(entity => entity.Id > cursorId);
        }

        return filteredQuery;
    }

    private static IOrderedQueryable<TEntity> SortByEntityId<TEntity>(IQueryable<TEntity> filteredQuery, NavigationDirection navigation, PageSortDirection sort)
        where TEntity : SourceKnownEntity
    {
        IOrderedQueryable<TEntity> orderedQuery;
        if (navigation == NavigationDirection.Previous)
        {
            orderedQuery = sort == PageSortDirection.DescendingByCreatedAt
                ? filteredQuery.OrderBy(entity => entity.Id)
                : filteredQuery.OrderByDescending(entity => entity.Id);
        }
        else
        {
            orderedQuery = sort == PageSortDirection.AscendingByCreatedAt
                ? filteredQuery.OrderBy(entity => entity.Id)
                : filteredQuery.OrderByDescending(entity => entity.Id);
        }

        return orderedQuery;
    }

    private static async Task<IReadOnlyList<TEntity>> GetEntitiesAsync<TEntity>(IQueryable<TEntity> query, PaginationRequest request,
        NavigationDirection navigation, PageSortDirection sort, CancellationToken cancellationToken)
        where TEntity : SourceKnownEntity
    {
        IReadOnlyList<TEntity> items = request.IsPageJump()
            ? await query.Skip(request.GetSkipSize()).Take(request.PageSize.Size + 1).ToListAsync(cancellationToken)
            : await query.Take(request.PageSize.Size + 1).ToListAsync(cancellationToken);

        if (navigation == NavigationDirection.Previous)
        {
            items = sort == PageSortDirection.AscendingByCreatedAt
                ? items.OrderBy(entity => entity.Id).ToArray()
                : items.OrderByDescending(entity => entity.Id).ToArray();
        }

        return items;
    }
}