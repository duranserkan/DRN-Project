using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tag;

namespace Sample.Application.Services.QA.Tags;

public interface ITagService : ISourceKnownService<TagEntity, TagResponse, TagPostRequest>;

[Scoped<ITagService>]
public class TagService(ITagRepository repository)
    : SourceKnownService<TagEntity, TagResponse, TagPostRequest>(repository), ITagService
{
    public override Task<TagResponse> CreateResponseAsync(TagEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(entity.Model);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new TagResponse
        {
            Id = entity.EntityId,
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt,
            Name = entity.Name,
            Model = new TagModelResponse
            {
                BoolValue = entity.Model.BoolValue,
                StringValue = entity.Model.StringValue,
                Max = entity.Model.Max,
                Min = entity.Model.Min,
                Other = entity.Model.Other,
                Date = entity.Model.Date,
                Type = entity.Model.Type
            }
        });
    }

    public override Task<TagEntity> CreateEntityAsync(TagPostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Model is null)
            throw ExceptionFor.Validation("Tag model is required.");
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
