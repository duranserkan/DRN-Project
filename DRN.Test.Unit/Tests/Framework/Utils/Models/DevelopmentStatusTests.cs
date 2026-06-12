using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Models;

namespace DRN.Test.Unit.Tests.Framework.Utils.Models;

public class DevelopmentStatusTests
{
    private static DbContextChangeModel CreateChangeModel() => new("TestContext", ["Migration1"], ["Migration1"],
        new DbContextChangeModelFlags(false, false, false));

    private static DbContextChangeModel CreatePrototypeChangeModel() => new("TestContext", [], [],
        new DbContextChangeModelFlags(true, true, false));

    private static void ConfigureEnvironment(DrnTestContextUnit context, AppEnvironment environment, bool autoMigrateDevelopment, bool autoMigrateStaging,
        bool prototype = false)
    {
        var section = nameof(DrnDevelopmentSettings);
        context.AddToConfiguration("Environment", environment.ToString());
        context.AddToConfiguration(section, nameof(DrnDevelopmentSettings.AutoMigrateDevelopment), autoMigrateDevelopment.ToString());
        context.AddToConfiguration(section, nameof(DrnDevelopmentSettings.AutoMigrateStaging), autoMigrateStaging.ToString());
        context.AddToConfiguration(section, nameof(DrnDevelopmentSettings.Prototype), prototype.ToString());
    }

    [Theory]
    [DataInlineUnit(AppEnvironment.Development, true, false, true)]
    [DataInlineUnit(AppEnvironment.Development, false, true, false)]
    [DataInlineUnit(AppEnvironment.Staging, true, false, false)]
    [DataInlineUnit(AppEnvironment.Staging, false, true, true)]
    [DataInlineUnit(AppEnvironment.Production, true, true, false)]
    public void Migrate_Flag_Should_Reflect_Environment_And_AutoMigrate_Settings(DrnTestContextUnit context,
        AppEnvironment environment, bool autoMigrateDevelopment, bool autoMigrateStaging, bool migrationEnabled)
    {
        ConfigureEnvironment(context, environment, autoMigrateDevelopment, autoMigrateStaging);

        var status = context.GetRequiredService<DevelopmentStatus>();
        var model = CreateChangeModel();
        status.AddChangeModel(model);

        model.Flags.Migrate.Should().Be(migrationEnabled);
    }

    [Theory]
    [DataInlineUnit(AppEnvironment.Development, true)]
    [DataInlineUnit(AppEnvironment.Staging, false)]
    [DataInlineUnit(AppEnvironment.Production, false)]
    public void Prototype_Recreate_Flag_Should_Only_Be_Enabled_In_Development(DrnTestContextUnit context,
        AppEnvironment environment, bool recreateEnabled)
    {
        ConfigureEnvironment(context, environment, autoMigrateDevelopment: true, autoMigrateStaging: true, prototype: true);

        var status = context.GetRequiredService<DevelopmentStatus>();
        var model = CreatePrototypeChangeModel();
        status.AddChangeModel(model);

        model.Flags.RecreatePrototypeDatabaseForPendingModelChanges.Should().Be(recreateEnabled);
    }

    [Theory]
    [DataInlineUnit(false, false, true)]
    [DataInlineUnit(false, true, true)]
    [DataInlineUnit(true, false, false)]
    [DataInlineUnit(true, true, true)]
    public void Prototype_Recreate_Flag_Should_Respect_Applied_Migrations(DrnTestContextUnit context,
        bool hasAppliedMigration, bool usePrototypeModeWhenMigrationExists, bool recreateEnabled)
    {
        ConfigureEnvironment(context, AppEnvironment.Development, autoMigrateDevelopment: true, autoMigrateStaging: false, prototype: true);

        var status = context.GetRequiredService<DevelopmentStatus>();
        string[] appliedMigrations = hasAppliedMigration ? ["InitialMigration"] : [];
        var model = new DbContextChangeModel("TestContext", ["InitialMigration"], appliedMigrations,
            new DbContextChangeModelFlags(true, true, usePrototypeModeWhenMigrationExists));
        status.AddChangeModel(model);

        model.Flags.RecreatePrototypeDatabaseForPendingModelChanges.Should().Be(recreateEnabled);
    }
}
