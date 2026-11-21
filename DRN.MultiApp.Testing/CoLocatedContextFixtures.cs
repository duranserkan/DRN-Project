using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Attributes;
using Microsoft.EntityFrameworkCore;

namespace DRN.MultiApp.Testing;

public readonly struct CoLocatedFirstApp : IAppId
{
    public const byte Value = 111;
    public static byte AppId => Value;
}

public readonly struct CoLocatedSecondApp : IAppId
{
    public const byte Value = 112;
    public static byte AppId => Value;
}

public enum CoLocatedEntityTypes : byte
{
    ModelEntity = 1,
    NonModelEntity = 2
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CoLocatedEntityTypeAttribute<TApp>(CoLocatedEntityTypes entityType)
    : EntityTypeAttribute<TApp>((byte)entityType) where TApp : IAppId;

[CoLocatedEntityType<CoLocatedFirstApp>(CoLocatedEntityTypes.ModelEntity)]
public sealed class CoLocatedFirstEntity : SourceKnownEntity;

[CoLocatedEntityType<CoLocatedSecondApp>(CoLocatedEntityTypes.ModelEntity)]
public sealed class CoLocatedSecondEntity : SourceKnownEntity;

[CoLocatedEntityType<CoLocatedFirstApp>(CoLocatedEntityTypes.NonModelEntity)]
public sealed class CoLocatedNonModelDomainEntity : SourceKnownEntity;

public static class CoLocatedPrivateEntityFixture
{
    public static Type EntityType => typeof(PrivateEntity);

    private sealed class PrivateEntity : SourceKnownEntity;
}

public class CoLocatedFirstContext(DbContextOptions<CoLocatedFirstContext> options) : DbContext(options)
{
    public DbSet<CoLocatedFirstEntity> FirstEntities => Set<CoLocatedFirstEntity>();
}

public class CoLocatedSecondContext(DbContextOptions<CoLocatedSecondContext> options) : DbContext(options)
{
    [IgnoreLog]
    public DbSet<CoLocatedSecondEntity> SecondEntities => Set<CoLocatedSecondEntity>();
}
