using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain;

public class Dto
{
    [JsonIgnore] //JsonIgnore is added to prevent entity serialization
    [JsonInclude] //JsonInclude is added to make constructor happy during deserialization
#pragma warning disable CS0169
    private SourceKnownEntity? _entity; // Field is never used, entity and _entity added to share same constructor with serializer and application code 
#pragma warning restore CS0169

    public Dto(SourceKnownEntity? entity = null)
    {
        if (entity is null)
            return;

        Id = entity.EntityId;
        CreatedAt = entity.CreatedAt;
        ModifiedAt = entity.ModifiedAt;
    }

    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}