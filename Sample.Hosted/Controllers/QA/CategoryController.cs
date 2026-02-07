using Microsoft.EntityFrameworkCore;
using Sample.Contract.QA.Categories;
using Sample.Domain.QA.Categories;
using Sample.Infra.QA;

namespace Sample.Hosted.Controllers.QA;

[ApiController]
[Route(QaApiFor.ControllerRouteTemplate)]
public class CategoryController(QAContext context) : ControllerBase
{
    [HttpGet]
    public async Task<CategoryDto[]> GetAsync()
    {
        var categories = await context.Categories.ToArrayAsync();

        return categories.Select(c => c.ToDto()).ToArray();
    }

    [HttpGet("{id:guid}")]
    public async Task<CategoryDto> GetAsync([FromRoute] Guid id)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.EntityIdSource.EntityId == id);
        if (category == null) throw ExceptionFor.NotFound($"Category: {id}");

        return category.ToDto();
    }

    [HttpPost]
    public async Task<CategoryDto> PostAsync([FromBody] CategoryPostRequest request)
    {
        var category = new Category(request.Name);
        context.Categories.Add(category);

        await context.SaveChangesAsync();

        return category.ToDto();
    }
}