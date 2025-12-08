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
    public async Task<Category[]> GetAsync()
    {
        var categories = await context.Categories.ToArrayAsync();

        return categories;
    }

    [HttpGet("{id:long}")]
    public async Task<Category> GetAsync([FromRoute] long id)
    {
        var category = await context.Categories.FindAsync(id);
        if (category == null) throw new NotFoundException($"Category: {id}");

        return category;
    }

    [HttpPost]
    public async Task<Category> PostAsync([FromBody] CategoryPostRequest request)
    {
        var category = new Category(request.Name);
        context.Categories.Add(category);

        await context.SaveChangesAsync();

        return category;
    }
}