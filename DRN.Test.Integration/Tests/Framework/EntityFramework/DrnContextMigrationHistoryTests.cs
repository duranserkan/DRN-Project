using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.Testing.Contexts.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DRN.Test.Integration.Tests.Framework.EntityFramework;

public class DrnContextMigrationHistoryTests
{
    private const string MigrationId = "20260828000000_BeforeMigrationSourcesWereRemoved";
    private const string SentinelValue = "database-must-not-be-recreated";

    [Theory]
    [DataInline]
    public async Task PostStartupValidation_Should_Not_Delete_Migrated_Database_When_Migration_Assembly_Is_Empty(DrnTestContext testContext)
    {
        testContext.AddToConfiguration(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.AutoMigrateDevelopment)), bool.TrueString);
        testContext.AddToConfiguration(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.Prototype)), bool.TrueString);

        var registration = new DrnContextServiceRegistrationAttribute();
        registration.ServiceRegistration(testContext.ServiceCollection, typeof(MigrationHistoryGuardContext).Assembly);

        var container = await testContext.ContainerContext.Postgres.Isolated.StartAsync();
        var contexts = PostgresContext.SetConnectionStrings(testContext, container);
        var dbContext = contexts.OfType<MigrationHistoryGuardContext>().Single();

        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Sentinels.Add(new MigrationHistoryGuardSentinel { Value = SentinelValue });
        await dbContext.SaveChangesAsync();

        var historyRepository = dbContext.GetService<IHistoryRepository>();
        await historyRepository.CreateIfNotExistsAsync();
        await dbContext.Database.ExecuteSqlRawAsync(historyRepository.GetInsertScript(new HistoryRow(MigrationId, ProductInfo.GetVersion())));

        dbContext.Database.GetMigrations().Should().BeEmpty();
        (await dbContext.Database.GetAppliedMigrationsAsync()).Should().ContainSingle().Which.Should().Be(MigrationId);

        var validation = () => registration.PostStartupValidationAsync(dbContext, testContext);

        await validation.Should().ThrowAsync<ConfigurationException>();

        dbContext.ChangeTracker.Clear();
        (await dbContext.Sentinels.SingleAsync()).Value.Should().Be(SentinelValue);
    }
}

[MigrationHistoryGuardContextOptions]
public sealed class MigrationHistoryGuardContext : DrnContext<MigrationHistoryGuardContext>
{
    public MigrationHistoryGuardContext(DbContextOptions<MigrationHistoryGuardContext> options) : base(options)
    {
    }

    public MigrationHistoryGuardContext() : base(null)
    {
    }

    public DbSet<MigrationHistoryGuardSentinel> Sentinels { get; set; }
}

public sealed class MigrationHistoryGuardSentinel
{
    public int Id { get; set; }
    public string Value { get; set; } = null!;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class MigrationHistoryGuardContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
{
    public override bool UsePrototypeMode { get; set; } = true;
}
