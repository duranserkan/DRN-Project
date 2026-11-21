using DRN.Framework.EntityFramework.Attributes;

namespace DRN.Nexus.Infra.NexusIdentity;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusIdentityContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute;
