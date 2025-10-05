using DRN.Framework.Utils.Data.Encodings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class AppSecuritySettingsTests
{
    [Theory]
    [DataInlineUnit]
    public void SecuritySettings_Should_BeValid(TestContextUnit testContext)
    {
        var securitySettings = testContext.GetRequiredService<IAppSecuritySettings>();

        var decodedEncryptionKey = securitySettings.AppEncryptionKey.Decode();
        decodedEncryptionKey.Length.Should().Be(32);
        
        var decodedHashKey = securitySettings.AppHashKey.Decode();
        decodedHashKey.Length.Should().Be(32);
        
        var decodedAppKey = securitySettings.AppKey.Decode();
        decodedAppKey.Length.Should().Be(32);
    }
}