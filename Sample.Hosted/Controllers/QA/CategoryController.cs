using DRN.Framework.Hosting.Controllers;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Category;

namespace Sample.Hosted.Controllers.QA;

[Route(ControllerRoutes.QA)]
[Authorize(Policy = Get.Policy.QaApi)]
public class CategoryController(ICategoryRepository repository)
    : SourceKnownController<CategoryEntity, CategoryDto, CategoryPostRequest>(repository)
{
    protected override CategoryDto ToDto(CategoryEntity entity) => entity.ToDto();

    protected override Task<CategoryEntity> CreateEntityAsync(CategoryPostRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new CategoryEntity(request.Name));

    [NonAction]
    public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => base.DeleteAsync(id, cancellationToken);

    [NonAction]
    public override Task<CategoryDto[]> GetManyAsync(Guid[] ids, CancellationToken cancellationToken = default)
        => base.GetManyAsync(ids, cancellationToken);

    [NonAction]
    public override Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => base.AnyAsync(cancellationToken);

    [NonAction]
    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
        => base.CountAsync(cancellationToken);
}
