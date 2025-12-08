using DRN.Framework.SharedKernel.Domain.Pagination;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace Sample.Hosted.Controllers.QA;

//https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-9.0#controllerbase-class
[ApiController]
[Route(QaApiFor.ControllerRouteTemplate)]
public class TagController(ITagRepository repository) : ControllerBase
{
    [HttpGet("Paginate")]
    public async Task<PaginationResultModel<TagDto>> PaginateAsync(
        [FromQuery] int pageSize = PageSize.SizeDefault,
        [FromQuery] int maxSize = PageSize.MaxSizeDefault,
        [FromQuery] long totalCount = -1,
        [FromQuery] bool updateTotalCount = false,
        [FromQuery] PageSortDirection direction = PageSortDirection.Ascending)
    {
        var request = PaginationRequest.DefaultWith(pageSize, maxSize, direction: direction, totalCount: totalCount, updateTotalCount: updateTotalCount);
        var result = await repository.PaginateAsync(request);

        return result.ToModel(tag => tag.ToDto());
    }

    [HttpGet("PaginateWithQuery")]
    public async Task<PaginationResultModel<TagDto>> PaginateWithQueryAsync([FromQuery] PaginationResultInfo? resultInfo,
        [FromQuery] int jumpTo = 1, [FromQuery] bool updateTotalCount = false)
    {
        var result = await repository.PaginateAsync(resultInfo, jumpTo, updateTotalCount: updateTotalCount);

        return result.ToModel(tag => tag.ToDto());
    }

    [HttpPost("PaginateWithBody")]
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
        var location = Get.Endpoint.QA.Tag.GetAsync.Path(tag.EntityId);

        return Created(location, tag.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> DeleteAsync([FromRoute] Guid id)
    {
        await repository.DeleteAsync(id);

        return NoContent();
    }
}