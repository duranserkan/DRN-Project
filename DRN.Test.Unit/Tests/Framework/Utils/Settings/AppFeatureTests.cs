namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class AppFeatureTests
{
    [Theory]
    [DataInline(true, false, true, false)]
    [DataInline(false, true, false, true)]
    public void Features_Should_BeValid(TestContext testContext, bool skipValidation, bool temporary, bool launch, bool prototype)
    {
        var section = nameof(DrnDevelopmentSettings);
        testContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.SkipValidation), skipValidation.ToString());
        testContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.TemporaryApplication), temporary.ToString());
        testContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.LaunchExternalDependencies), launch.ToString());
        testContext.AddToConfiguration(section, nameof(DrnDevelopmentSettings.PrototypingMode), prototype.ToString());


        var appSetting = testContext.GetRequiredService<IAppSettings>();
        var developmentSettings = appSetting.DevelopmentSettings;

        developmentSettings.TemporaryApplication.Should().Be(temporary);
        developmentSettings.SkipValidation.Should().Be(skipValidation);
        developmentSettings.LaunchExternalDependencies.Should().Be(launch);
        developmentSettings.PrototypingMode.Should().Be(prototype);
    }
}