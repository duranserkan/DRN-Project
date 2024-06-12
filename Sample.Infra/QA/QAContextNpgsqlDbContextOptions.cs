using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Sample.Infra.QA;

public class QAContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions(NpgsqlDbContextOptionsBuilder builder)
        => builder.CommandTimeout(30);
}