using DRN.Framework.SharedKernel.Domain;
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

[EntityType<CoLocatedFirstApp>(1)]
public sealed class CoLocatedFirstEntity : SourceKnownEntity;

[EntityType<CoLocatedSecondApp>(1)]
public sealed class CoLocatedSecondEntity : SourceKnownEntity;

[EntityType<CoLocatedFirstApp>(2)]
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
    public DbSet<CoLocatedSecondEntity> SecondEntities => Set<CoLocatedSecondEntity>();
}
