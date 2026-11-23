using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Sample.Contract.QA.Tags;
using Sample.Domain.QA.Tag;

namespace Sample.Application.Services.QA.Tags;

public interface ITagService : ISourceKnownService<TagEntity, TagResponse, TagPostRequest>;

[Scoped<ITagService>]
public class TagService(ITagRepository repository)
    : SourceKnownService<TagEntity, TagResponse, TagPostRequest>(repository), ITagService
{
    protected override Task<TagResponse> CreateResponseCoreAsync(TagEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity.Model);

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

    protected override Task<TagEntity> CreateEntityCoreAsync(TagPostRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Model is null)
        {
            const string message = "Tag model is required.";
            ScopeContext.Problem.Detail = message;
            ScopeContext.Problem.Validation(message);
        }

        if (request.Model.Type != TagType.User)
        {
            const string message = "Only User tag type is permitted.";
            ScopeContext.Problem.Detail = message;
            ScopeContext.Problem.Validation(message);
        }

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
