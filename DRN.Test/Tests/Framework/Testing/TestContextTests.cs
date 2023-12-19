using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Questions;
using Sample.Infra;
using Sample.Infra.Repositories.QA;

namespace DRN.Test.Tests.Framework.Testing;

public class TestContextTests
{
    [Theory]
    [DataInline]
    public async Task TestContext_Should_Migrate_DbContexts(TestContext context)
    {
        context.ServiceCollection.AddSampleInfraServices();
        context.StartPostgreSQL();
        var qaContext = context.GetRequiredService<QAContext>();
        var appliedMigrations = await qaContext.Database.GetAppliedMigrationsAsync();

        appliedMigrations.Any().Should().BeTrue();
    }
}