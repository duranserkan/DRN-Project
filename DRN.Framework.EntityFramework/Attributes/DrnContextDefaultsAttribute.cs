using DRN.Framework.SharedKernel.Conventions;
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

public class DrnContextDefaultsAttribute : NpgsqlDbContextOptionsAttribute
{
    public DrnContextDefaultsAttribute() => FrameworkDefined = true;

    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider) => builder
        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        .MigrationsAssembly(typeof(TContext).Assembly.FullName)
        .MigrationsHistoryTable($"{typeof(TContext).Name.ToSnakeCase()}_history", "__entity_migrations");

    public override void ConfigureNpgsqlDataSource<TContext>(NpgsqlDataSourceBuilder builder, IServiceProvider? serviceProvider)
    {
        builder.EnableParameterLogging(false);
        builder.ConfigureJsonOptions(JsonConventions.DefaultOptions);

        var appSettings = serviceProvider?.GetRequiredService<IAppSettings>() ?? AppSettings.Instance;
        var defaultApplicationName = $"{appSettings.ApplicationName}_{typeof(TContext).Name}";

        builder.ConnectionStringBuilder.ApplicationName ??= defaultApplicationName;
    }

    public override void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
    {
        // Each integration test will create its own internal service provider
        if (TestEnvironment.TestContextEnabled)
            builder.ConfigureWarnings(warningsConfigurationBuilder =>
                warningsConfigurationBuilder.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        var scopedLog = serviceProvider?.GetRequiredService<IScopedLog>();
        builder
            .UseSnakeCaseNamingConvention()
            .LogTo(LogWarning, [DbLoggerCategory.Name], LogLevel.Warning);
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