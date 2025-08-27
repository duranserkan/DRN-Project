namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResultModel<TModel>(PaginationResultInfo info, IReadOnlyList<TModel> items)
{
    public PaginationResultInfo Info { get; } = info;
    public IReadOnlyList<TModel> Items { get; } = items;

    //todo add tests
    public PaginationResultModel<TMapped> ToModel<TMapped>(Func<TModel, TMapped> mapper) => new(Info, Items.Select(mapper).ToArray());
}