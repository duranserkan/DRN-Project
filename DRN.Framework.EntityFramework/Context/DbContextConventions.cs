using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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
    public const int DefaultPort = 5432;

    private static readonly ConcurrentDictionary<Type, NpgsqlDbContextOptionsAttribute[]> AttributeCache = new();

    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    private static class ContextAttributeCache<TContext>
    {
        internal static NpgsqlDbContextOptionsAttribute[]? Attributes;
    }

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

    public static NpgsqlDataSourceBuilder ConfigureNpgsqlDataSourceBuilder<TContext>(
        this NpgsqlDataSourceBuilder dataSourceBuilder, IServiceProvider? serviceProvider = null)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureNpgsqlDataSource<TContext>(dataSourceBuilder, serviceProvider);

        return dataSourceBuilder;
    }

    private static DbContextOptionsBuilder ConfigureDbContextOptions<TContext>(
        this DbContextOptionsBuilder optionsBuilder, IServiceProvider? serviceProvider)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureDbContextOptions<TContext>(optionsBuilder, serviceProvider);

        if (serviceProvider is null)
            return optionsBuilder;

        var coreOptions = optionsBuilder.Options.FindExtension<CoreOptionsExtension>();
        var seeder = coreOptions?.Seeder;
        var asyncSeeder = coreOptions?.AsyncSeeder;

        optionsBuilder.UseSeeding((context, changesPerformed) =>
        {
            if (seeder is not null)
                seeder(context, changesPerformed);
            else
                asyncSeeder?.Invoke(context, changesPerformed, CancellationToken.None).GetAwaiter().GetResult();

            DrnContextServiceRegistrationHelper.SeedDataAsync(context, serviceProvider,
                serviceProvider.GetRequiredService<IAppSettings>()).GetAwaiter().GetResult();
        });
        optionsBuilder.UseAsyncSeeding(async (context, changesPerformed, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (asyncSeeder is not null)
                await asyncSeeder(context, changesPerformed, cancellationToken).ConfigureAwait(false);
            else
                seeder?.Invoke(context, changesPerformed);

            await DrnContextServiceRegistrationHelper.SeedDataAsync(context, serviceProvider,
                serviceProvider.GetRequiredService<IAppSettings>(), cancellationToken).ConfigureAwait(false);
        });

        return optionsBuilder;
    }

    private static void ConfigureNpgsqlDbContextOptions<TContext>(this NpgsqlDbContextOptionsBuilder optionsBuilder, IServiceProvider? serviceProvider)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureNpgsqlOptions<TContext>(optionsBuilder, serviceProvider);
    }

    public static NpgsqlDbContextOptionsAttribute[] GetContextAttributes<TContext>()
    {
        var attributes = Volatile.Read(ref ContextAttributeCache<TContext>.Attributes);
        if (attributes is not null)
            return attributes;

        // Share the runtime cache's instances and allow retries if attribute construction fails.
        attributes = GetContextAttributes(typeof(TContext));
        return Interlocked.CompareExchange(ref ContextAttributeCache<TContext>.Attributes, attributes, null) ?? attributes;
    }

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
