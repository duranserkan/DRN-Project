namespace DRN.Framework.SharedKernel.Domain;

public readonly record struct EntityTypeId(byte AppId, byte EntityType)
{
}