using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Category;

namespace Sample.Application.Services.QA.Categories;

public interface ICategoryService : ISourceKnownService<CategoryEntity, CategoryResponse, CategoryPostRequest>
{
    Task<CategoryEntity?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default);
}

[Scoped<ICategoryService>]
public class CategoryService(ICategoryRepository repository)
    : SourceKnownService<CategoryEntity, CategoryResponse, CategoryPostRequest>(repository), ICategoryService
{
    protected override Task<CategoryResponse> CreateResponseCoreAsync(CategoryEntity entity, CancellationToken cancellationToken = default)
        => Task.FromResult(new CategoryResponse
        {
            Id = entity.EntityId,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            Name = entity.Name
        });

    protected override Task<CategoryEntity> CreateEntityCoreAsync(CategoryPostRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new CategoryEntity(request.Name));

    public Task<CategoryEntity?> GetOrDefaultAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Repository.CancelWhen(cancellationToken);
        return Repository.GetOrDefaultAsync(id, validate: false);
    }
}
