using DRN.Framework.EntityFramework.Attributes;

namespace Sample.Infra.SampleIdentity;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class SampleIdentityContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute;
