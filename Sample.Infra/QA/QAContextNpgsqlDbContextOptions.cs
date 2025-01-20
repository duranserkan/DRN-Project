using DRN.Framework.EntityFramework.Attributes;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sample.Infra.QA;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class QAContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
        => builder.CommandTimeout(30);
}