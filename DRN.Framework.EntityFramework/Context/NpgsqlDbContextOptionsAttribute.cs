using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace DRN.Framework.EntityFramework.Context;

public abstract class NpgsqlDbContextOptionsAttribute : Attribute
{
    internal bool FrameworkDefined = false;
    public abstract void ConfigureNpgSqlOptions(NpgsqlDbContextOptionsBuilder builder);
}

public class SplitQueryAttribute : NpgsqlDbContextOptionsAttribute
{
    public SplitQueryAttribute() => FrameworkDefined = true;

    public override void ConfigureNpgSqlOptions(NpgsqlDbContextOptionsBuilder builder)
        => builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
}