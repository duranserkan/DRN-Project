using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Domain;

public class Dto
{
    [JsonConstructor]
    private Dto()
    {
    }

    public Dto(SourceKnownEntity entity)
    {
        Id = entity.EntityId;
        CreatedAt = entity.CreatedAt;
        ModifiedAt = entity.ModifiedAt;
    }

    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
}