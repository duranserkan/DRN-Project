using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Domain.QA.Categories;

namespace Sample.Application.Services;

public interface ICategoryService
{
    Task<Category?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

[Scoped<ICategoryService>]
public class CategoryService(ICategoryRepository repository) : ICategoryService
{
    public Task<Category?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        repository.CancelWhen(cancellationToken);
        return repository.GetOrDefaultAsync(id, validate: false);
    }
}
