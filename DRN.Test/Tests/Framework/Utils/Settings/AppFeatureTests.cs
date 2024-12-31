namespace DRN.Test.Tests.Framework.Utils.Settings;

public class AppFeatureTests
{
    [Theory]
    [DataInline(true, false, true, false)]
    [DataInline(false, true, false, true)]
    public void Features_Should_BeValid(TestContext testContext, bool skipValidation, bool temporary, bool launch, bool prototype)
    {
        var section = nameof(DrnAppFeatures);
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.SkipValidation), skipValidation.ToString());
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.TemporaryApplication), temporary.ToString());
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.LaunchExternalDependencies), launch.ToString());
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.PrototypingMode), prototype.ToString());


        var appSetting = testContext.GetRequiredService<IAppSettings>();
        var features = appSetting.Features;

        features.TemporaryApplication.Should().Be(temporary);
        features.SkipValidation.Should().Be(skipValidation);
        features.LaunchExternalDependencies.Should().Be(launch);
        features.PrototypingMode.Should().Be(prototype);
    }
}