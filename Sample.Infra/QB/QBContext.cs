using System.ComponentModel.DataAnnotations;
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

    //Added to test prototype mode
    //public DbSet<TestEntity> TestEntity { get; set; }
}

[EntityTypeId((int)SampleEntityTypeIds.TestEntity)]
public class TestEntity : AggregateRoot
{
    public long TestValue { get; set; }

    [MaxLength(100)]
    public string TestValueString { get; set; } = string.Empty;
}