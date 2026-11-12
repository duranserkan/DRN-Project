using DRN.Framework.SharedKernel.Domain.Pagination;
using DRN.Framework.Hosting.Extensions;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace Sample.Hosted.Controllers.QA;

//https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-9.0#controllerbase-class
[ApiController]
[Route(QaApiFor.ControllerRouteTemplate)]
public class TagController(ITagRepository repository) : ControllerBase
{
    [HttpGet("Paginate")]
    [EndpointDescription("Fetch the first page. The total is unknown unless updateTotalCount is true. maxSize is capped at 1000. Use a continuation endpoint to request another page.")]
    public async Task<PaginationResultModel<TagDto>> PaginateAsync(
        [FromQuery] int pageSize = PageSize.SizeDefault,
        [FromQuery] int maxSize = PageSize.MaxSizeDefault,
        [FromQuery] bool updateTotalCount = false,
        [FromQuery] PageSortDirection direction = PageSortDirection.Ascending)
    {
        // A first-page total must come from the server, not the query string.
        var request = PaginationRequest.DefaultWith(pageSize, maxSize, direction, updateTotalCount: updateTotalCount);
        var result = await repository.PaginateAsync(request);

        return result.ToModel(tag => tag.ToDto());
    }

    [HttpGet("PaginateWithOptions")]
    [EndpointDescription("Fetch the first page using flat pagination options. Set updateTotalCount to count on the server.")]
    public async Task<PaginationResultModel<TagDto>> PaginateWithOptionsAsync([FromQuery] PaginationOptions options)
    {
        var request = PaginationRequest.DefaultWith(options);
        var result = await repository.PaginateAsync(request);

        return result.ToModel(tag => tag.ToDto());
    }

    [HttpGet("PaginateWithQuery")]
    [EndpointDescription("jumpTo defaults to 1. The target is clamped to at least 1 and within 10 pages of the previous page. updateTotalCount defaults to false.")]
    public async Task<PaginationResultModel<TagDto>> PaginateWithQueryAsync([FromQuery] PaginationResultInfo? resultInfo,
        [FromQuery] int jumpTo = 1, [FromQuery] bool updateTotalCount = false)
    {
        var result = await repository.PaginateAsync(resultInfo, jumpTo, updateTotalCount: updateTotalCount);

        return result.ToModel(tag => tag.ToDto());
    }

    [HttpPost("PaginateWithBody")]
    [EndpointDescription("jumpTo defaults to 1. The target is clamped to at least 1 and within 10 pages of the previous page. updateTotalCount defaults to false.")]
    public async Task<PaginationResultModel<TagDto>> PaginateWithBodyAsync([FromBody] PaginationResultInfo? resultInfo,
        [FromQuery] int jumpTo = 1,
        [FromQuery] bool updateTotalCount = false)
    {
        var result = await repository.PaginateAsync(resultInfo, jumpTo, updateTotalCount: updateTotalCount);

        return result.ToModel(tag => tag.ToDto());
    }

    [HttpGet("{id:guid}")]
    public async Task<TagDto> GetAsync([FromRoute] Guid id)
    {
        var result = await repository.GetAsync(id);

        return result.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> PostAsync([FromBody] TagPostRequest request)
    {
        var tag = new Tag(request.Name)
        {
            Model = request.Model
        };

        await repository.CreateAsync(tag);
        var location = Url.Endpoint(Get.Endpoint.QA.Tag.GetAsync, new { id = tag.EntityId.ToString("N") });
        return Created(location, tag.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> DeleteAsync([FromRoute] Guid id)
    {
        await repository.DeleteAsync(id);

        return NoContent();
    }
}
