using System.Diagnostics.CodeAnalysis;
using DRN.Framework.EntityFramework.Attributes;

namespace DRN.Nexus.Infra.NexusIdentity;

[SuppressMessage("SonarQube", "S3993",
    Justification = "Intentionally inherits AttributeUsage from NpgsqlDbContextOptionsAttribute to keep the usage policy defined in one place.")]
public sealed class NexusIdentityContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute;
