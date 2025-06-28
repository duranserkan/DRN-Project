namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResultModel<TModel, TEntity>(PaginationResult<TEntity> paginationResult, Func<TEntity, TModel> mapper) : PaginationResultBase(paginationResult)
    where TEntity : SourceKnownEntity
{
    public IReadOnlyList<TModel> Items { get; } = paginationResult.Items.Select(mapper).ToArray();
}