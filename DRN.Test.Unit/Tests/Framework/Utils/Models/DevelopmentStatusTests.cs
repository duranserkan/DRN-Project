using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Models;

namespace DRN.Test.Unit.Tests.Framework.Utils.Models;

public class DevelopmentStatusTests
{
    private static DbContextChangeModel CreateChangeModel() => new("TestContext", ["Migration1"], ["Migration1"],
        new DbContextChangeModelFlags(false, false, false));

    private static void ConfigureEnvironment(DrnTestContextUnit context, AppEnvironment environment, bool autoMigrateDevelopment, bool autoMigrateStaging)
    {
        var section = nameof(DrnDevelopmentSettings);
        context.AddToConfiguration("Environment", environment.ToString());
        context.AddToConfiguration(section, nameof(DrnDevelopmentSettings.AutoMigrateDevelopment), autoMigrateDevelopment.ToString());
        context.AddToConfiguration(section, nameof(DrnDevelopmentSettings.AutoMigrateStaging), autoMigrateStaging.ToString());
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
}