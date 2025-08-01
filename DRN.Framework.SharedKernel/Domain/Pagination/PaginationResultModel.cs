namespace DRN.Framework.SharedKernel.Domain.Pagination;

public class PaginationResultModel<TModel>(PaginationResultInfo resultInfo, IReadOnlyList<TModel> items)
{
    public PaginationResultInfo Info { get; } = resultInfo;
    public IReadOnlyList<TModel> Items { get; } = items;

    //todo add tests
    public PaginationResultModel<TMapped> ToModel<TMapped>(Func<TModel, TMapped> mapper) => new(Info, Items.Select(mapper).ToArray());
}