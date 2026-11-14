using DRN.Framework.Hosting.Controllers;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Categories;
using Sample.Hosted.Settings;

namespace Sample.Hosted.Controllers.QA;

[Route(QaApiFor.ControllerRouteTemplate)]
[Authorize(Policy = IdentitySettings.QaApiPolicy)]
public class CategoryController(ICategoryRepository repository)
    : SourceKnownController<Category, CategoryDto, CategoryPostRequest>(repository)
{
    protected override CategoryDto ToDto(Category entity) => entity.ToDto();

    protected override Task<Category> CreateEntityAsync(CategoryPostRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new Category(request.Name));

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
