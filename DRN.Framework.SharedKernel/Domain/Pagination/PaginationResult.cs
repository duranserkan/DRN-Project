namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResult<TEntity> : PaginationResultBase where TEntity : SourceKnownEntity
{
    public PaginationResult(IReadOnlyList<TEntity> items, PaginationRequest request, long totalCount = -1)
    {
        Request = request;
        var excessCount = request.PageSize.Size + 1;
        var hasExcessCount = items.Count == excessCount;

        Items = items;
        if (hasExcessCount)
        {
            Items = request.NavigationDirection != NavigationDirection.Next
                ? items.Skip(1).Take(request.PageSize.Size).ToArray()
                : items.Take(request.PageSize.Size).ToArray();
        }

        PageNumber = request.PageNumber;
        PageSize = request.PageSize.Size;

        if (items.Count > 0)
        {
            var newestEntity = Items.Max()!;
            var oldestEntity = Items.Min()!;

            if (request.PageCursor.SortDirection == PageSortDirection.AscendingByCreatedAt)
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

        HasPrevious = PageNumber > 1;
        HasNext = request.NavigationDirection == NavigationDirection.Previous ||
                  (request.NavigationDirection == NavigationDirection.Next && hasExcessCount);

        ItemCount = Items.Count;
        Total = new PaginationTotal(totalCount, PageSize);
        TotalCountUpdated = request.UpdateTotalCount;

        if (!Total.CountSpecified)
        {
            if (request is { MarkAsHasNextOnRefresh: true, IsPageRefresh: true })
                HasNext = true;
            return;
        }

        HasNext = PageNumber < Total.Pages;
    }

    public IReadOnlyList<TEntity> Items { get; }
    public PaginationResultModel<TModel, TEntity> ToModel<TModel>(Func<TEntity, TModel> mapper) => new(this, mapper);
}