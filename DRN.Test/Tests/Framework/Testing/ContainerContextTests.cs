using Microsoft.EntityFrameworkCore;
using Sample.Infra;
using Sample.Infra.QA;

namespace DRN.Test.Tests.Framework.Testing;

public class ContainerContextPostgresTests
{
    [Theory]
    [DataInline]
    public async Task ContainerContext_Should_Migrate_DbContexts(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        await context.ContainerContext.StartPostgresAndApplyMigrationsAsync();
        var qaContext = context.GetRequiredService<QAContext>();
        var appliedMigrations = await qaContext.Database.GetAppliedMigrationsAsync();

        appliedMigrations.Any().Should().BeTrue();
    }
}

public class ContainerContextRabbitMqTests
{
    [Theory]
    [DataInline]
    public async Task ContainerContext_Should_Create_RabbitMq_Container(TestContext context)
    {
        var container = await context.ContainerContext.StartRabbitMqAsync();
        var connectionString = container.GetConnectionString();
        connectionString.Should().NotBeNull();
    }
}