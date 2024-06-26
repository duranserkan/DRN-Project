using DRN.Framework.EntityFramework.Attributes;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sample.Infra.QA;

public class QAContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder)
        => builder.CommandTimeout(30);
}