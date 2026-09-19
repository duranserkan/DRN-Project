using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Entity;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Sample.Domain.QB;

namespace Sample.Infra.QB;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class QBContextNpgsqlDbContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override void ConfigureNpgsqlOptions<TContext>(NpgsqlDbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
        => builder.CommandTimeout(30);

    public override bool UsePrototypeMode { get; set; } = false;
}

//Added to the test multiple context support
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

public interface IQBTestEntityRepository : ISourceKnownRepository<QBTestEntity>;

public class QBTestEntityRepository(QBContext context, IEntityUtils utils)
    : SourceKnownRepository<QBContext, QBTestEntity>(context, utils), IQBTestEntityRepository;
