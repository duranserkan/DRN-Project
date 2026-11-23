using DRN.Framework.Hosting.Controllers;
using Sample.Application.Services.QA.Tags;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tag;

namespace Sample.Hosted.Controllers.QA;

[Route(ControllerRoutes.QA)]
[Authorize(Policy = Get.Policy.QaApi)]
public class TagController(ITagService service)
    : SourceKnownController<TagEntity, TagResponse, TagPostRequest>(service)
{
    [NonAction]
    public override Task<TagResponse[]> GetManyAsync(Guid[] ids, CancellationToken cancellationToken = default)
        => base.GetManyAsync(ids, cancellationToken);

    [NonAction]
    public override Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => base.AnyAsync(cancellationToken);

    [NonAction]
    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
        => base.CountAsync(cancellationToken);
}
