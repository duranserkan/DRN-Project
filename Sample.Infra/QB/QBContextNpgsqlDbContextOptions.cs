using DRN.Framework.EntityFramework.Attributes;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sample.Infra.QB;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class QBContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
        => builder.CommandTimeout(30);

    public override bool UsePrototypeMode { get; set; } = true;
}