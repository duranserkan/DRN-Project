using DRN.Framework.Testing.Contexts.RabbitMQ;
using Microsoft.EntityFrameworkCore;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.Testing;

public class ContainerContextPostgresTests
{
    [Theory]
    [DataInline]
    public async Task ContainerContext_Should_Migrate_DbContexts(DrnTestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.Postgres.ApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();
        var appliedMigrations = await qaContext.Database.GetAppliedMigrationsAsync();

        appliedMigrations.Any().Should().BeTrue();
    }
}

public class ContainerContextRabbitMqTests
{
    [TheoryDebuggerOnly]
    [DataInline]
    public async Task ContainerContext_Should_Create_RabbitMq_Container(DrnTestContext _)
    {
        var container = await RabbitMQContext.StartAsync();
        var connectionString = container.GetConnectionString();
        connectionString.Should().NotBeNull();
    }
}