using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.EntityFramework.Context;

public static class DbContextConventions
{
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