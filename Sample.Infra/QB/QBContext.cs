using DRN.Framework.SharedKernel.Domain;
using Sample.Domain;

namespace Sample.Infra.QB;

//Added to test multiple context support
[QBContextNpgsqlDbContextOptions]
public class QBContext : DrnContext<QBContext>
{
    public QBContext(DbContextOptions<QBContext> options) : base(options)
    {
    }

    public QBContext() : base(null)
    {
    }

    public DbSet<TestEntity> TestEntity { get; set; }
}

[EntityTypeId((int)SampleEntityTypeIds.TestEntity)]
public class TestEntity : AggregateRoot
{
    public long TestValue { get; set; }
    public string TestValueString { get; set; } = string.Empty;
}