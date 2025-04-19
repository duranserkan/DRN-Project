namespace DRN.Framework.SharedKernel.Domain;

public readonly record struct SourceKnownId(long Id, byte AppId, byte AppInstanceId, uint InstanceId, DateTimeOffset CreatedAt)
{
    public bool Equals(SourceKnownId other) => Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}

public readonly record struct SourceKnownEntityId(SourceKnownId Source, Guid EntityId, ushort EntityTypeId, bool Valid)
{
    public bool Equals(SourceKnownEntityId other) => EntityId == other.EntityId;

    public override int GetHashCode() => EntityId.GetHashCode();
}