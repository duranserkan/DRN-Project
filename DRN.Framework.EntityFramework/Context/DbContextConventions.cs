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
    public const string DefaultUsername = "drn";
    public const string DefaultDatabase = "drn";
    public const string DefaultHost = "drn";
    public const string DefaultPort = "5432";

    private static readonly ConcurrentDictionary<Type, NpgsqlDbContextOptionsAttribute[]> AttributeCache = new();

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(
        DbContextOptionsBuilder? contextOptions = null, IServiceProvider? serviceProvider = null) where TContext : DbContext
        => (contextOptions ?? new DbContextOptionsBuilder<TContext>())
            .ConfigureDbContextOptions<TContext>(serviceProvider)
            .UseNpgsql(npgsqlOptions => npgsqlOptions.ConfigureNpgsqlDbContextOptions<TContext>(serviceProvider));

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(NpgsqlDataSource dataSource,
        DbContextOptionsBuilder? contextOptions = null, IServiceProvider? serviceProvider = null) where TContext : DbContext
        => (contextOptions ?? new DbContextOptionsBuilder<TContext>())
            .ConfigureDbContextOptions<TContext>(serviceProvider)
            .UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.ConfigureNpgsqlDbContextOptions<TContext>(serviceProvider));

    private static DbContextOptionsBuilder ConfigureDbContextOptions<TContext>(
        this DbContextOptionsBuilder optionsBuilder, IServiceProvider? serviceProvider)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureDbContextOptions<TContext>(optionsBuilder, serviceProvider);

        return optionsBuilder;
    }

    private static void ConfigureNpgsqlDbContextOptions<TContext>(this NpgsqlDbContextOptionsBuilder optionsBuilder, IServiceProvider? serviceProvider)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureNpgsqlOptions<TContext>(optionsBuilder, serviceProvider);
    }

    public static NpgsqlDbContextOptionsAttribute[] GetContextAttributes<TContext>() => GetContextAttributes(typeof(TContext));

    public static NpgsqlDbContextOptionsAttribute[] GetContextAttributes<TContext>(TContext context) where TContext : DbContext
        => GetContextAttributes(context.GetType());

    public static NpgsqlDbContextOptionsAttribute[] GetContextAttributes(Type contextType)
    {
        if (AttributeCache.TryGetValue(contextType, out var attributes))
            return attributes;

        attributes = AttributeCache.GetOrAdd(contextType, type => type
            .GetCustomAttributes<NpgsqlDbContextOptionsAttribute>()
            .OrderByDescending(attribute => attribute.FrameworkDefined).ToArray());

        return attributes;
    }

    public static IReadOnlyDictionary<Type, NpgsqlDbContextOptionsAttribute[]> InitializeAll(Type[] types)
    {
        _ = types.Select(GetContextAttributes).ToArray();

        return AttributeCache.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}