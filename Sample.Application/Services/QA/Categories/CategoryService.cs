using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Domain.QA.Category;

namespace Sample.Application.Services.QA.Categories;

public interface ICategoryService
{
    Task<CategoryEntity?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

[Scoped<ICategoryService>]
public class CategoryService(ICategoryRepository repository) : ICategoryService
{
    public Task<CategoryEntity?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        repository.CancelWhen(cancellationToken);
        return repository.GetOrDefaultAsync(id, validate: false);
    }
}
