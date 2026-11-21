using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Category;

namespace Sample.Application.Services.QA.Categories;

public interface ICategoryService : ISourceKnownService<CategoryEntity, CategoryDto, CategoryPostRequest>
{
    Task<CategoryEntity?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

[Scoped<ICategoryService>]
public class CategoryService(ICategoryRepository repository)
    : SourceKnownService<CategoryEntity, CategoryDto, CategoryPostRequest>(repository), ICategoryService
{
    public override Task<CategoryEntity> CreateEntityAsync(CategoryPostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new CategoryEntity(request.Name));
    }

    public Task<CategoryEntity?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Repository.CancelWhen(cancellationToken);
        return Repository.GetOrDefaultAsync(id, validate: false);
    }
}
