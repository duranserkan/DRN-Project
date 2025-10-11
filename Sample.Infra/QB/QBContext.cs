using System.ComponentModel.DataAnnotations;
using DRN.Framework.EntityFramework;
using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

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

    //Added to test prototype mode
    //public DbSet<TestEntity> TestEntity { get; set; }
}

public enum TestEntityTypes : byte
{
    TestEntity = 255
}

[EntityType((int)TestEntityTypes.TestEntity)]
public class TestEntity : AggregateRoot
{
    public long TestValue { get; init; }

    [MaxLength(100)]
    public string TestValueString { get; init; } = string.Empty;
}

// public class TestEntityConfig: IEntityTypeConfiguration<TestEntity>
// {
//     public void Configure(EntityTypeBuilder<TestEntity> builder)
//     {
//     }
// }

public interface ITestEntityRepository : ISourceKnownRepository<TestEntity>;

public class TestEntityRepository(QBContext context, IEntityUtils utils)
    : SourceKnownRepository<QBContext, TestEntity>(context, utils), ITestEntityRepository;