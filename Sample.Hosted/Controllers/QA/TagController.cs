using DRN.Framework.Hosting.Controllers;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tag;

namespace Sample.Hosted.Controllers.QA;

[Route(ControllerRoutes.QA)]
[Authorize(Policy = Get.Policy.QaApi)]
public class TagController(ITagRepository repository)
    : SourceKnownController<TagEntity, TagDto, TagPostRequest>(repository)
{
    protected override TagDto ToDto(TagEntity entity) => entity.ToDto();

    protected override Task<TagEntity> CreateEntityAsync(TagPostRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new TagEntity(request.Name) { Model = request.Model });

    [NonAction]
    public override Task<TagDto[]> GetManyAsync(Guid[] ids, CancellationToken cancellationToken = default)
        => base.GetManyAsync(ids, cancellationToken);

    [NonAction]
    public override Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => base.AnyAsync(cancellationToken);

    [NonAction]
    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
        => base.CountAsync(cancellationToken);
}
