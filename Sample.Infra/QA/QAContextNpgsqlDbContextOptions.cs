using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sample.Infra.QA;

public class QAContextNpgsqlDbContextOptions : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgSqlOptions(NpgsqlDbContextOptionsBuilder builder)
        => builder.CommandTimeout(30);
}