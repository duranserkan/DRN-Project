using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResult<TEntity> : PaginationResultBase where TEntity : SourceKnownEntity
{
    [SetsRequiredMembers]
    public PaginationResult(IReadOnlyList<TEntity> items, PaginationRequest request, long totalCount = -1)
    {
        Request = request;
        var excessCount = request.PageSize.Size + 1;
        var hasExcessCount = items.Count == excessCount;

        Items = items;
        if (hasExcessCount)
        {
            Items = request.NavigationDirection == PageNavigationDirection.Previous
                ? items.Skip(1).Take(request.PageSize.Size).ToArray()
                : items.Take(request.PageSize.Size).ToArray();
        }

        if (items.Count > 0)
        {
            var newestEntity = Items.Max()!;
            var oldestEntity = Items.Min()!;

            if (request.PageCursor.SortDirection == PageSortDirection.Ascending)
            {
                LastId = newestEntity.EntityId;
                FirstId = oldestEntity.EntityId;
            }
            else
            {
                LastId = oldestEntity.EntityId;
                FirstId = newestEntity.EntityId;
            }
        }
        else
        {
            LastId = Guid.Empty;
            FirstId = Guid.Empty;
        }

        HasPrevious = Request.PageNumber > 1;
        HasNext = request.NavigationDirection == PageNavigationDirection.Previous ||
                  (request.NavigationDirection == PageNavigationDirection.Next && hasExcessCount);

        ItemCount = Items.Count;
        Total = new PaginationTotal(totalCount, Request.PageSize.Size);
        TotalCountUpdated = request.UpdateTotalCount;

        if (!Total.CountSpecified)
        {
            if (request is { MarkAsHasNextOnRefresh: true, IsPageRefresh: true })
                HasNext = true;
            return;
        }

        HasNext = HasNext || Request.PageNumber < Total.Pages;
    }

    public IReadOnlyList<TEntity> Items { get; }

    public PaginationResultModel<TModel> ToModel<TModel>(Func<TEntity, TModel> mapper) => new(ToResultInfo(), Items.Select(mapper).ToArray());
    public PaginationResultModel<TEntity> ToModel() => ToModel<TEntity>(entity => entity);
}