using DRN.Framework.EntityFramework.Attributes;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sample.Infra.QB;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class QBContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
        => builder.CommandTimeout(30);

    public override bool UsePrototypeMode { get; set; } = false;
}

// Demonstrates an entityless context alongside the application's persistence contexts.
[QBContextNpgsqlDbContextOptions]
public class QBContext : DrnContext<QBContext>
{
    public QBContext(DbContextOptions<QBContext> options) : base(options)
    {
    }

    public QBContext() : base(null)
    {
    }
}
