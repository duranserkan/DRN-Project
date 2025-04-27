using Microsoft.EntityFrameworkCore;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Infra.QA;

namespace Sample.Hosted.Controllers.QA;

[ApiController]
[Route("Api/QA/[controller]")]
public class TagController(QAContext context) : ControllerBase
{
    [HttpGet]
    public async Task<Tag[]> GetAsync()
    {
        var tags = await context.Tags.ToArrayAsync();

        return tags;
    }

    [HttpGet("{id:long}")]
    public async Task<Tag> GetAsync([FromRoute] long id)
    {
        var tag = await context.Tags.FindAsync(id);
        if (tag == null) throw new NotFoundException($"Category: {id}");

        return tag;
    }

    [HttpPost]
    public async Task<Tag> PostAsync([FromBody] TagPostRequest request)
    {
        var tag = new Tag(request.Name)
        {
            Model = request.Model
        };
        context.Tags.Add(tag);

        await context.SaveChangesAsync();

        return tag;
    }
}