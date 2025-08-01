using DRN.Framework.SharedKernel.Domain.Pagination;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;

namespace Sample.Hosted.Controllers.QA;

[ApiController]
[Route("Api/QA/[controller]")]
public class TagController(ITagRepository repository) : ControllerBase
{
    [HttpPost("Pagination")]
    public async Task<PaginationResultModel<Tag>> GetAsync([FromBody] PaginationRequest? request)
    {
        var result = await repository.PaginateAsync(request ?? PaginationRequest.Default);

        return result;
    }

    [HttpPost("Pagination/Next")]
    public async Task<PaginationResultModel<Tag>> GetNextAsync([FromBody] PaginationResultInfo resultInfo)
    {
        var result = await repository.PaginateAsync(resultInfo.RequestNextPage());

        return result;
    }

    [HttpPost("Pagination/Previous")]
    public async Task<PaginationResultModel<Tag>> GetPreviousAsync([FromBody] PaginationResultInfo resultInfo)
    {
        var result = await repository.PaginateAsync(resultInfo.RequestPreviousPage());

        return result;
    }
    
    [HttpPost("Pagination/Refresh")]
    public async Task<PaginationResultModel<Tag>> GetRefreshAsync([FromBody] PaginationResultInfo resultInfo)
    {
        var result = await repository.PaginateAsync(resultInfo.RequestRefresh());

        return result;
    }

    [HttpPost("Pagination/Jump/{pageNumber:int}")]
    public async Task<PaginationResultModel<Tag>> GetJumpAsync([FromBody] PaginationResultInfo resultInfo, [FromRoute] int pageNumber)
    {
        var result = await repository.PaginateAsync(resultInfo.RequestPage(pageNumber));

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