namespace DRN.Framework.SharedKernel.Domain;

public readonly record struct SourceKnownIdInfo(byte AppId, byte AppInstanceId, uint InstanceId, DateTimeOffset CreatedAt, long Id)
{
    public bool Equals(SourceKnownIdInfo other) => Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}

public readonly record struct EntityIdInfo(SourceKnownIdInfo IdInfo, Guid EntityId, ushort EntityTypeId, short Residue, int Code, bool Valid)
{
    public bool Equals(EntityIdInfo other) => EntityId == other.EntityId;

    public override int GetHashCode() => EntityId.GetHashCode();
}