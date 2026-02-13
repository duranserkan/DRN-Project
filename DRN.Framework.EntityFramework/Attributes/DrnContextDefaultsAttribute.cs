using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public class DrnContextDefaultsAttribute : NpgsqlDbContextOptionsAttribute
{
    public DrnContextDefaultsAttribute() => FrameworkDefined = true;

    /// <summary>
        /// Configure Npgsql-specific EF Core options for the given DbContext type.
        /// </summary>
        /// <param name="builder">The Npgsql DbContext options builder to configure.</param>
        /// <param name="serviceProvider">Optional service provider; may be null and is not used by this implementation.</param>
        /// <remarks>
        /// Sets query splitting behavior to SplitQuery, configures the migrations assembly to the assembly containing <typeparamref name="TContext"/>,
        /// sets the migrations history table to "{contextName}_history" in the "__entity_migrations" schema (context name converted to snake_case),
        /// and fixes the PostgreSQL version to 18.2.
        /// </remarks>
        public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider) => builder
        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        .MigrationsAssembly(typeof(TContext).Assembly.FullName)
        .MigrationsHistoryTable($"{typeof(TContext).Name.ToSnakeCase()}_history", "__entity_migrations")
        .SetPostgresVersion(18,2);

    /// <summary>
    /// Configure the Npgsql data source builder: disable parameter logging, apply default JSON conventions, and ensure a sensible ApplicationName is set.
    /// </summary>
    /// <param name="builder">The NpgsqlDataSourceBuilder to configure.</param>
    /// <param name="serviceProvider">Service provider used to resolve IAppSettings for computing the default application name.</param>
    public override void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider serviceProvider)
    {
        builder.EnableParameterLogging(false);
        builder.ConfigureJsonOptions(JsonConventions.DefaultOptions);

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        var defaultApplicationName = $"{appSettings.ApplicationName}_{typeof(TContext).Name}";

        builder.ConnectionStringBuilder.ApplicationName ??= defaultApplicationName;
    }

    public override void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
    {
        base.ConfigureDbContextOptions<TContext>(builder, serviceProvider);
        
        // Each integration test will create its own internal service provider
        //ManyServiceProvidersCreatedWarning is expected in integration tests
        //DrnContextServiceRegistrationAttribute.PostStartupValidationAsync will test this case on a normal startup
        if (TestEnvironment.DrnTestContextEnabled) 
            builder.ConfigureWarnings(warnings => { warnings.Log(CoreEventId.ManyServiceProvidersCreatedWarning); }); 

        var scopedLog = serviceProvider?.GetRequiredService<IScopedLog>();
        builder.UseSnakeCaseNamingConvention().LogTo(LogWarning, [DbLoggerCategory.Name], LogLevel.Warning);
        return;

        void LogWarning(string message)
        {
            if (scopedLog == null)
                Console.WriteLine(message);
            else
                scopedLog.AddWarning(message);
        }
    }
}