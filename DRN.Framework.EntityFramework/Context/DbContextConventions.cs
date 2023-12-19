using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Context;

public static class DbContextConventions
{
    public static DbContextOptionsBuilder DbContextGetOptionsBuilder<TContext>(string connectionString, string contextName,
        DbContextOptionsBuilder? builder = null)
        where TContext : DbContext
    {
        builder ??= new DbContextOptionsBuilder();
        var dbContextOptionsBuilder = builder.UseNpgsql(connectionString, options =>
            options.MigrationsAssembly(typeof(TContext).Assembly.FullName).MigrationsHistoryTable($"__{contextName}MigrationsHistory"));
        dbContextOptionsBuilder.UseSnakeCaseNamingConvention();

        return dbContextOptionsBuilder;
    }
}