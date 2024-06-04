using System.Reflection;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Context;

public static class DbContextConventions
{
    public const string DevPasswordKey = "postgres-password";
    public const string DevHostKey = "DrnContext_DevHost";
    public const string DevPortKey = "DrnContext_DevPort";
    public const string DevUsernameKey = "DrnContext_DevUsername";
    public const string DevDatabaseKey = "DrnContext_DevDatabase";
    public const string DefaultUsername = "postgres";
    public const string DefaultDatabase = "drnDb";
    public const string DefaultHost = "postgresql";
    public const string DefaultPort = "5432";

    private static readonly Dictionary<Type, NpgsqlDbContextOptionsAttribute[]> AttributeCache = new();

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>
        (string connectionString, string contextName, DbContextOptionsBuilder? builder = null) where TContext : DbContext
    {
        return (builder ?? new DbContextOptionsBuilder<TContext>())
            .UseSnakeCaseNamingConvention()
            .LogTo(Console.WriteLine, [DbLoggerCategory.Name], LogLevel.Warning)
            .UseNpgsql(connectionString, optionsBuilder =>
            {
                optionsBuilder
                    .MigrationsAssembly(typeof(TContext).Assembly.FullName)
                    .MigrationsHistoryTable($"{contextName.ToSnakeCase()}_history", "__entity_migrations")
                    .ApplyNpgsqlDbContextOptions<TContext>();
            });
    }

    private static void ApplyNpgsqlDbContextOptions<TContext>(this NpgsqlDbContextOptionsBuilder optionsBuilder) where TContext : DbContext
    {
        var attributes = typeof(TContext).GetAttributesFromCache();

        foreach (var attribute in attributes)
            attribute.ConfigureNpgSqlOptions(optionsBuilder);
    }

    private static NpgsqlDbContextOptionsAttribute[] GetAttributesFromCache(this Type type)
    {
        if (AttributeCache.TryGetValue(type, out var attributes))
            return attributes;

        //service validation will trigger this on startup no race condition is expected
        attributes = type.GetCustomAttributes<NpgsqlDbContextOptionsAttribute>()
            .OrderByDescending(attribute => attribute.FrameworkDefined).ToArray();
        AttributeCache[type] = attributes;

        return attributes;
    }

    public static TContext CreateDbContext<TContext>(this string[] args) where TContext : DbContext
    {
        var contextName = typeof(TContext).Name;
        var connectionString = args.FirstOrDefault()!;
        var optionsBuilder = UpdateDbContextOptionsBuilder<TContext>(connectionString, contextName);

        return (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
    }

    public static void ModelCreatingDefaults(this DbContext dbContext, ModelBuilder modelBuilder)
    {
        var context = dbContext.GetType();
        modelBuilder.HasDefaultSchema(context.Name.ToSnakeCase());
        modelBuilder.ApplyConfigurationsFromAssembly(context.Assembly, configuration => configuration.Namespace!.Contains(context.Namespace!));
        modelBuilder.Ignore<DomainEvent>();
    }

    public static void MarkEntities(this DbContext dbContext)
    {
        var entries = dbContext.ChangeTracker
            .Entries()
            .Where(e => e.Entity is Entity && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();

        foreach (var entityEntry in entries)
        {
            var entity = (Entity)entityEntry.Entity;
            switch (entityEntry.State)
            {
                case EntityState.Added:
                    entity.MarkAsCreated();
                    break;
                case EntityState.Modified:
                    entity.MarkAsModified();
                    break;
                case EntityState.Deleted:
                    entity.MarkAsDeleted();
                    break;
            }
        }
    }
}