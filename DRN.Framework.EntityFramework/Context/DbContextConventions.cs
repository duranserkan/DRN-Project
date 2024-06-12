using System.Reflection;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
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

    private static readonly Dictionary<string, NpgsqlDbContextOptionsAttribute[]> AttributeCache = new();

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(
        DbContextOptionsBuilder? contextOptionsBuilder = null) where TContext : DbContext
    {
        contextOptionsBuilder ??= new DbContextOptionsBuilder<TContext>();
        contextOptionsBuilder
            .UseSnakeCaseNamingConvention()
            .LogTo(Console.WriteLine, [DbLoggerCategory.Name], LogLevel.Warning) //todo: check for improved logging
            .ConfigureDbContextOptions<TContext>()
            .UseNpgsql(npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder
                    .MigrationsAssembly(typeof(TContext).Assembly.FullName)
                    .MigrationsHistoryTable($"{typeof(TContext).Name.ToSnakeCase()}_history", "__entity_migrations")
                    .ConfigureNpgsqlDbContextOptions<TContext>();
            });

        return contextOptionsBuilder;
    }

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(NpgsqlDataSource dataSource,
        DbContextOptionsBuilder? contextOptionsBuilder = null) where TContext : DbContext
    {
        contextOptionsBuilder ??= new DbContextOptionsBuilder<TContext>();
        contextOptionsBuilder
            .UseSnakeCaseNamingConvention()
            .LogTo(Console.WriteLine, [DbLoggerCategory.Name], LogLevel.Warning) //todo: check for improved logging
            .ConfigureDbContextOptions<TContext>()
            .UseNpgsql(dataSource, npgsqlDbContextOptionsBuilder =>
            {
                npgsqlDbContextOptionsBuilder
                    .MigrationsAssembly(typeof(TContext).Assembly.FullName)
                    .MigrationsHistoryTable($"{typeof(TContext).Name.ToSnakeCase()}_history", "__entity_migrations")
                    .ConfigureNpgsqlDbContextOptions<TContext>();
            });

        return contextOptionsBuilder;
    }

    private static DbContextOptionsBuilder ConfigureDbContextOptions<TContext>(
        this DbContextOptionsBuilder optionsBuilder)
        where TContext : DbContext
    {
        var attributes = GetAttributesFromCache<TContext>();
        foreach (var attribute in attributes)
            attribute.ConfigureDbContextOptions(optionsBuilder);

        return optionsBuilder;
    }

    private static void ConfigureNpgsqlDbContextOptions<TContext>(this NpgsqlDbContextOptionsBuilder optionsBuilder)
        where TContext : DbContext
    {
        var attributes = GetAttributesFromCache<TContext>();
        foreach (var attribute in attributes)
            attribute.ConfigureNpgsqlOptions(optionsBuilder);
    }

    public static NpgsqlDbContextOptionsAttribute[] GetAttributesFromCache<TContext>()
    {
        var type = typeof(TContext);
        if (AttributeCache.TryGetValue(type.Name, out var attributes))
            return attributes;

        //service validation will trigger this on startup no race condition is expected
        attributes = type.GetCustomAttributes<NpgsqlDbContextOptionsAttribute>()
            .OrderByDescending(attribute => attribute.FrameworkDefined).ToArray();
        AttributeCache[type.Name] = attributes;

        return attributes;
    }

    public static TContext CreateDbContext<TContext>(this string[] args) where TContext : DbContext
    {
        var connectionString = args.FirstOrDefault()!;
        DbContextOptionsBuilder optionsBuilder;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            optionsBuilder = UpdateDbContextOptionsBuilder<TContext>();
        }
        else
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            var dataSource = dataSourceBuilder.Build();
            optionsBuilder = UpdateDbContextOptionsBuilder<TContext>(dataSource);
        }

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