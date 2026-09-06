using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class NexusAppSettingsTests
{
    private const string SampleKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void NexusAppSettings_Validate_Should_Pass_When_MacType_Is_Default()
    {
        var key = new NexusKey(SampleKey, ByteEncoding.Utf8) { Default = true };
        var settings = new NexusAppSettings
        {
            Keys = [key]
        };

        settings.MacType.Should().Be(NexusMacType.Blake3);
        var action = () => settings.Validate();

        action.Should().NotThrow();
        settings.MacType.Should().Be(NexusMacType.Blake3);
    }

    [Fact]
    public void NexusAppSettings_Validate_Should_Throw_When_MacType_Is_Invalid()
    {
        var key = new NexusKey(SampleKey, ByteEncoding.Utf8) { Default = true };
        var settings = new NexusAppSettings
        {
            MacType = (NexusMacType)999,
            Keys = [key]
        };

        var action = () => settings.Validate();

        var exception = action.Should().ThrowExactly<ConfigurationException>().Which;
        exception.Message.Should().Be("NexusAppSettings.MacType '999' is invalid");
    }
}
