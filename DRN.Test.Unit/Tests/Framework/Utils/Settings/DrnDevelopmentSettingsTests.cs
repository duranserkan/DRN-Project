namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class DrnDevelopmentSettingsTests
{
    [Theory]
    [DataInlineUnit(true, false, true, false)]
    [DataInlineUnit(false, true, false, true)]
    public void Development_Features_Should_BeValid(DrnTestContextUnit drnTestContext, bool skipValidation, bool temporary, bool launch, bool prototype)
    {
        var section = nameof(DrnDevelopmentSettings);
        drnTestContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.SkipValidation), skipValidation.ToString());
        drnTestContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.TemporaryApplication), temporary.ToString());
        drnTestContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.LaunchExternalDependencies), launch.ToString());
        drnTestContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.Prototype), prototype.ToString());


        var appSetting = drnTestContext.GetRequiredService<IAppSettings>();
        var developmentSettings = appSetting.DevelopmentSettings;

        developmentSettings.TemporaryApplication.Should().Be(temporary);
        developmentSettings.SkipValidation.Should().Be(skipValidation);
        developmentSettings.LaunchExternalDependencies.Should().Be(launch);
        developmentSettings.Prototype.Should().Be(prototype);
    }
}