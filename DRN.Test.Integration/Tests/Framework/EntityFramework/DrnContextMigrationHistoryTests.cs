using DRN.Framework.EntityFramework.Attributes;
using DRN.Framework.EntityFramework.Context;
using DRN.Framework.EntityFramework.Context.Interceptors;
using DRN.Framework.EntityFramework.Extensions;
using DRN.Framework.SharedKernel;
using DRN.Framework.Testing.Contexts.Postgres;
using DRN.Framework.Utils.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DRN.Test.Integration.Tests.Framework.EntityFramework;

public class DrnContextMigrationHistoryTests
{
    private const string MigrationId = "20260828000000_BeforeMigrationSourcesWereRemoved";
    private const string SentinelValue = "database-must-not-be-recreated";

    /// <summary>
    /// Verifies that prototype database recreation is blocked when the target database contains migration history,
    /// even if local source migration files or model snapshots are missing from the executing assembly.
    /// <para>
    /// Relying on assembly-declared migrations to decide whether to query the database is dangerous: if migration files
    /// are deleted or absent (e.g., branch checkout or refactoring), an assembly-only check reports zero applied migrations.
    /// Pending-model detection would then mistakenly classify the database as unmigrated and invoke
    /// <c>EnsureDeletedAsync</c>, destroying existing database data.
    /// </para>
    /// </summary>
    [Theory]
    [DataInline]
    public async Task PostStartupValidation_Should_Not_Delete_Migrated_Database_When_Migration_Assembly_Is_Empty(DrnTestContext testContext)
    {
        testContext.AddToConfiguration(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.AutoMigrateDevelopment)), bool.TrueString);
        testContext.AddToConfiguration(DrnDevelopmentSettings.GetKey(nameof(DrnDevelopmentSettings.Prototype)), bool.TrueString);

        // Register the private test context directly along with required DrnContext interceptors
        // to avoid scanning the entire test assembly and polluting other integration tests.
        testContext.ServiceCollection.AddDbContextWithConventions<MigrationHistoryGuardContext>();
        testContext.ServiceCollection.TryAddSingleton<IDrnMaterializationInterceptor, DrnMaterializationInterceptor>();
        testContext.ServiceCollection.TryAddSingleton<IDrnSaveChangesInterceptor, DrnSaveChangesInterceptor>();
        testContext.ServiceCollection.TryAddSingleton<IPaginationUtils, PaginationUtils>();

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

        var registration = new DrnContextServiceRegistrationAttribute();
        var validation = () => registration.PostStartupValidationAsync(dbContext, testContext);

        await validation.Should().ThrowAsync<ConfigurationException>()
            .WithMessage("*has pending model changes, but prototype recreation is blocked because migrations are applied to the database. Create migration or enable UsePrototypeModeWhenMigrationExists.*");

        dbContext.ChangeTracker.Clear();
        (await dbContext.Sentinels.SingleAsync()).Value.Should().Be(SentinelValue);
    }

    [MigrationHistoryGuardContextOptions]
    private sealed class MigrationHistoryGuardContext : DrnContext<MigrationHistoryGuardContext>
    {
        public MigrationHistoryGuardContext(DbContextOptions<MigrationHistoryGuardContext> options) : base(options)
        {
        }

        public MigrationHistoryGuardContext() : base(null)
        {
        }

        public DbSet<MigrationHistoryGuardSentinel> Sentinels { get; set; }
    }

    private sealed class MigrationHistoryGuardSentinel
    {
        public int Id { get; set; }
        public string Value { get; set; } = null!;
    }

    [AttributeUsage(AttributeTargets.Class)]
    private sealed class MigrationHistoryGuardContextOptionsAttribute : NpgsqlDbContextOptionsAttribute
    {
        public override bool UsePrototypeMode { get; set; } = true;
    }
}
