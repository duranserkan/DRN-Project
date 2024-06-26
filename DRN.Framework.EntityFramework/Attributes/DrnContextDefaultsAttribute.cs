using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Attributes;

public class DrnContextDefaultsAttribute : NpgsqlDbContextOptionsAttribute
{
    public DrnContextDefaultsAttribute() => FrameworkDefined = true;

    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder) => builder
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            .MigrationsAssembly(typeof(TContext).Assembly.FullName)
            .MigrationsHistoryTable($"{typeof(TContext).Name.ToSnakeCase()}_history", "__entity_migrations");

    public override void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder)
    {
        // Each integration test will create its own internal service provider
        if (TestEnvironment.TestContextEnabled)
            builder.ConfigureWarnings(warningsConfigurationBuilder =>
                warningsConfigurationBuilder.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder
            .UseSnakeCaseNamingConvention() //todo: check for improved logging
            .LogTo(Console.WriteLine, [DbLoggerCategory.Name], LogLevel.Warning);
    }
}