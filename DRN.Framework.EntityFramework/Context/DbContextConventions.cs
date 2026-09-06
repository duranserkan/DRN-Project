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

    [SuppressMessage("SonarQube", "S2326", Justification = "Generic cache per context type")]
    [SuppressMessage("SonarQube", "S2743", Justification = "Generic cache per context type")]
    [SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "Avoid infinite recursion into generic cache entry point")]
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    [SuppressMessage("ReSharper", "UnusedTypeParameter")]
    private static class ContextAttributeCache<TContext>
    {
        public static NpgsqlDbContextOptionsAttribute[] Attributes
        {
            get
            {
                var attributes = Volatile.Read(ref field);
                if (attributes is not null)
                    return attributes;

                // Share the runtime cache's instances and allow retries if attribute construction fails.
                attributes = GetContextAttributes(typeof(TContext));
                return Interlocked.CompareExchange(ref field, attributes, null) ?? attributes;
            }
        }
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
        var previousOptions = optionsBuilder.Options.FindExtension<CoreOptionsExtension>();
        if (previousOptions is not null)
        {
            // Restore custom callbacks before attributes run so repeated configuration cannot compose DRN wrappers.
            // EF stores null to clear callbacks despite non-nullable WithSeeding/WithAsyncSeeding parameters.
            if (previousOptions.Seeder?.Target is SeedingCallbacks previousSeeder)
                previousOptions = previousOptions.WithSeeding(previousSeeder.Seeder!);
            if (previousOptions.AsyncSeeder?.Target is SeedingCallbacks previousAsyncSeeder)
                previousOptions = previousOptions.WithAsyncSeeding(previousAsyncSeeder.AsyncSeeder!);
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(previousOptions);
        }

        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureDbContextOptions<TContext>(optionsBuilder, serviceProvider);

        if (serviceProvider is null)
            return optionsBuilder;

        var coreOptions = optionsBuilder.Options.FindExtension<CoreOptionsExtension>();
        var callbacks = new SeedingCallbacks(serviceProvider, coreOptions?.Seeder, coreOptions?.AsyncSeeder);
        optionsBuilder.UseSeeding(callbacks.Seed);
        optionsBuilder.UseAsyncSeeding(callbacks.SeedAsync);

        return optionsBuilder;
    }

    private sealed class SeedingCallbacks(
        IServiceProvider serviceProvider,
        Action<DbContext, bool>? seeder,
        Func<DbContext, bool, CancellationToken, Task>? asyncSeeder)
    {
        public Action<DbContext, bool>? Seeder { get; } = seeder;
        public Func<DbContext, bool, CancellationToken, Task>? AsyncSeeder { get; } = asyncSeeder;

        public void Seed(DbContext context, bool changesPerformed)
        {
            if (Seeder is not null)
                Seeder(context, changesPerformed);
            else
                AsyncSeeder?.Invoke(context, changesPerformed, CancellationToken.None).GetAwaiter().GetResult();

            DrnContextServiceRegistrationHelper.SeedDataAsync(context, serviceProvider,
                serviceProvider.GetRequiredService<IAppSettings>()).GetAwaiter().GetResult();
        }

        public async Task SeedAsync(DbContext context, bool changesPerformed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AsyncSeeder is not null)
                await AsyncSeeder(context, changesPerformed, cancellationToken).ConfigureAwait(false);
            else
                Seeder?.Invoke(context, changesPerformed);

            await DrnContextServiceRegistrationHelper.SeedDataAsync(context, serviceProvider,
                serviceProvider.GetRequiredService<IAppSettings>(), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ConfigureNpgsqlDbContextOptions<TContext>(this NpgsqlDbContextOptionsBuilder optionsBuilder, IServiceProvider? serviceProvider)
        where TContext : DbContext
    {
        foreach (var attribute in GetContextAttributes<TContext>())
            attribute.ConfigureNpgsqlOptions<TContext>(optionsBuilder, serviceProvider);
    }

    public static NpgsqlDbContextOptionsAttribute[] GetContextAttributes<TContext>()
        => ContextAttributeCache<TContext>.Attributes;

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
