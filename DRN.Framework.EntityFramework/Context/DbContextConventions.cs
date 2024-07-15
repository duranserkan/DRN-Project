using System.Collections.Concurrent;
using System.Reflection;
using DRN.Framework.EntityFramework.Attributes;
using Microsoft.EntityFrameworkCore;
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

    private static readonly ConcurrentDictionary<string, NpgsqlDbContextOptionsAttribute[]> AttributeCache = new();

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(
        DbContextOptionsBuilder? contextOptions = null, IServiceProvider? serviceProvider = null) where TContext : DbContext
        => (contextOptions ?? new DbContextOptionsBuilder<TContext>())
            .ConfigureDbContextOptions<TContext>(serviceProvider)
            .UseNpgsql(npgsqlOptions => npgsqlOptions.ConfigureNpgsqlDbContextOptions<TContext>());

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(NpgsqlDataSource dataSource,
        DbContextOptionsBuilder? contextOptions = null, IServiceProvider? serviceProvider = null) where TContext : DbContext
        => (contextOptions ?? new DbContextOptionsBuilder<TContext>())
            .ConfigureDbContextOptions<TContext>(serviceProvider)
            .UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.ConfigureNpgsqlDbContextOptions<TContext>());

    private static DbContextOptionsBuilder ConfigureDbContextOptions<TContext>(
        this DbContextOptionsBuilder optionsBuilder, IServiceProvider? serviceProvider)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureDbContextOptions<TContext>(optionsBuilder, serviceProvider);

        return optionsBuilder;
    }

    private static void ConfigureNpgsqlDbContextOptions<TContext>(this NpgsqlDbContextOptionsBuilder optionsBuilder)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureNpgsqlOptions<TContext>(optionsBuilder);
    }

    public static NpgsqlDbContextOptionsAttribute[] GetContextAttributes<TContext>()
    {
        var type = typeof(TContext);
        if (AttributeCache.TryGetValue(type.Name, out var attributes))
            return attributes;

        attributes = AttributeCache.GetOrAdd(type.Name,
            _ => type.GetCustomAttributes<NpgsqlDbContextOptionsAttribute>()
                .OrderByDescending(attribute => attribute.FrameworkDefined).ToArray());

        return attributes;
    }
}