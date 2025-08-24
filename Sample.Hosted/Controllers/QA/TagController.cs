using DRN.Framework.SharedKernel.Domain.Pagination;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace Sample.Hosted.Controllers.QA;

[ApiController]
[Route("Api/QA/[controller]")]
public class TagController(ITagRepository repository) : ControllerBase
{
    [HttpGet("Paginate")]
    public async Task<PaginationResultModel<Tag>> PaginateAsync(
        [FromQuery] int pageSize = PageSize.SizeDefault,
        [FromQuery] int maxSize = PageSize.MaxSizeDefault,
        [FromQuery] bool updateTotalCount = false,
        [FromQuery] PageSortDirection direction = PageSortDirection.Ascending)
    {
        var request = PaginationRequest.DefaultWith(pageSize, maxSize, updateTotalCount, direction);
        var result = await repository.PaginateAsync(request);

        return result;
    }

    [HttpGet("PaginateWithQuery")]
    public async Task<PaginationResultModel<Tag>> PaginateWithQueryAsync([FromQuery] PaginationResultInfo? resultInfo, [FromQuery] int jumpTo = 1)
    {
        var result = await repository.PaginateAsync(resultInfo, jumpTo);

        return result;
    }

    [HttpPost("PaginateWithBody")]
    public async Task<PaginationResultModel<Tag>> PaginateWithBodyAsync([FromBody] PaginationResultInfo? resultInfo, [FromQuery] int jumpTo = 1)
    {
        var result = await repository.PaginateAsync(resultInfo, jumpTo);

        return result;
    }

    [HttpGet("{id:guid}")]
    public async Task<Tag> GetAsync([FromRoute] Guid id)
    {
        var result = await repository.GetAsync(id);

        return result;
    }

    [HttpPost]
    public async Task<Tag> PostAsync([FromBody] TagPostRequest request)
    {
        var tag = new Tag(request.Name)
        {
            Model = request.Model
        };

        await repository.CreateAsync(tag);

        return tag;
    }

    [HttpDelete("{id:guid}")]
    public async Task<int> DeleteAsync([FromRoute] Guid id)
    {
        var count = await repository.DeleteAsync(id);

        return count;
    }
}