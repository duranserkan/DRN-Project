namespace DRN.Test.Tests.Framework.Utils.Settings;

public class AppFeatureTests
{
    [Theory]
    [DataInline(true, false, true)]
    [DataInline(false, true, false)]
    public void Features_Should_BeValid(TestContext testContext, bool skipValidation, bool temporary, bool launch)
    {
        var section = nameof(DrnAppFeatures);
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.SkipValidation), skipValidation.ToString());
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.TemporaryApplication), temporary.ToString());
        testContext.AddToConfiguration(section, nameof(DrnAppFeatures.LaunchExternalDependencies), launch.ToString());

        var appSetting = testContext.GetRequiredService<IAppSettings>();
        var features = appSetting.Features;

        features.TemporaryApplication.Should().Be(temporary);
        features.SkipValidation.Should().Be(skipValidation);
        features.LaunchExternalDependencies.Should().Be(launch);
    }
}