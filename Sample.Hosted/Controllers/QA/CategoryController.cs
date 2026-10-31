using DRN.Framework.Hosting.Extensions;
using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Utils.Entity;
using Microsoft.EntityFrameworkCore;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Categories;
using Sample.Infra.QA;

namespace Sample.Hosted.Controllers.QA;

[ApiController]
[Route(QaApiFor.ControllerRouteTemplate)]
public class CategoryController(QAContext context, IPaginationUtils pagination) : ControllerBase
{
    [HttpGet]
    public async Task<PaginationResultModel<CategoryDto>> PaginateAsync(
        [FromQuery] PaginationRequest? request = null, CancellationToken cancellationToken = default)
    {
        request ??= PaginationRequest.Default;
        // The endpoint owns the ceiling, including when callers send cursor metadata.
        var boundedRequest = new PaginationRequest(request.PageNumber,
            new PageSize(request.PageSize.Size, PageSize.MaxSizeDefault), request.PageCursor,
            updateTotalCount: request.UpdateTotalCount);
        var result = await pagination.GetResultAsync(context.Categories.AsNoTracking(), boundedRequest, cancellationToken);

        return result.ToModel(category => category.ToDto());
    }

    [HttpGet("{id:guid}")]
    public async Task<CategoryDto> GetAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var category = await context.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityIdSource.EntityId == id, cancellationToken);
        if (category == null)
        {
            ScopeContext.Problem.Type = CategoryProblemTypes.NotFound;
            ScopeContext.Problem.Title = "Category not found";
            ScopeContext.Problem.NotFound($"Category: {id}");
        }

        return category.ToDto();
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> PostAsync([FromBody] CategoryPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = new Category(request.Name);
        context.Categories.Add(category);

        await context.SaveChangesAsync(cancellationToken);

        var location = Url.Endpoint(Get.Endpoint.QA.Category.GetAsync, new { id = category.EntityId.ToString("N") });
        return Created(location, category.ToDto());
    }
}
