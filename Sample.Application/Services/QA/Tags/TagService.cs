using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tag;

namespace Sample.Application.Services.QA.Tags;

public interface ITagService : ISourceKnownService<TagEntity, TagDto, TagPostRequest>;

[Scoped<ITagService>]
public class TagService(ITagRepository repository)
    : SourceKnownService<TagEntity, TagDto, TagPostRequest>(repository), ITagService
{
    public override Task<TagEntity> CreateEntityAsync(TagPostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Model?.Type is null)
            throw ExceptionFor.Validation("Tag type is required.");
        if (request.Model.Type != TagType.User)
            throw ExceptionFor.Validation("Only User tag type is permitted.");

        return Task.FromResult(new TagEntity(request.Name)
        {
            Model = new TagValueModel
            {
                BoolValue = request.Model.BoolValue,
                StringValue = request.Model.StringValue,
                Max = request.Model.Max,
                Min = request.Model.Min,
                Other = request.Model.Other,
                Date = request.Model.Date,
                Type = request.Model.Type.Value
            }
        });
    }
}
