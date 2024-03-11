using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.EntityFramework.Context;

public static class DbContextConventions
{
    public const string AutoMigrateDevEnvironmentKey = "DrnContext_AutoMigrateDevEnvironment";
    public const string DevPasswordKey = "postgres-password";
    public const string DevHostKey = "DrnContext_DevHost";
    public const string DevPortKey = "DrnContext_DevPort";
    public const string DevUsernameKey = "DrnContext_DevUsername";
    public const string DevDatabaseKey = "DrnContext_DevDatabase";
    public const string DefaultUsername = "postgres";
    public const string DefaultDatabase = "drnDb";
    public const string DefaultHost = "postgresql";
    public const string DefaultPort = "5432";

    public static DbContextOptionsBuilder UpdateDbContextOptionsBuilder<TContext>(string connectionString, string contextName,
        DbContextOptionsBuilder? builder = null)
        where TContext : DbContext
    {
        builder ??= new DbContextOptionsBuilder<TContext>();
        return builder
            .UseNpgsql(connectionString, options => options
                .MigrationsAssembly(typeof(TContext).Assembly.FullName)
                .MigrationsHistoryTable($"__{contextName}MigrationsHistory"))
            .UseSnakeCaseNamingConvention()
            .LogTo(Console.WriteLine, [DbLoggerCategory.Name], LogLevel.Warning);
    }
}