using DRN.Framework.Hosting.Controllers;
using Sample.Application.Services.QA.Categories;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Category;

namespace Sample.Hosted.Controllers.QA;

[Route(ControllerRoutes.QA)]
[Authorize(Policy = Get.Policy.QaApi)]
public class CategoryController(ICategoryService service)
    : SourceKnownController<CategoryEntity, CategoryResponse, CategoryPostRequest>(service)
{
    [NonAction]
    public override Task<NoContentResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => base.DeleteAsync(id, cancellationToken);

    [NonAction]
    public override Task<CategoryResponse[]> GetManyAsync(Guid[] ids, CancellationToken cancellationToken = default)
        => base.GetManyAsync(ids, cancellationToken);

    [NonAction]
    public override Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => base.AnyAsync(cancellationToken);

    [NonAction]
    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
        => base.CountAsync(cancellationToken);
}
