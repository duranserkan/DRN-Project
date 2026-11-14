using DRN.Framework.Hosting.Controllers;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tags;
using Sample.Hosted.Settings;

namespace Sample.Hosted.Controllers.QA;

[Route(QaApiFor.ControllerRouteTemplate)]
[Authorize(Policy = IdentitySettings.QaApiPolicy)]
public class TagController(ITagRepository repository)
    : SourceKnownController<Tag, TagDto, TagPostRequest>(repository)
{
    protected override TagDto ToDto(Tag entity) => entity.ToDto();

    protected override Task<Tag> CreateEntityAsync(TagPostRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new Tag(request.Name) { Model = request.Model });

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
