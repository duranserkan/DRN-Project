using Sample.Hosted.Settings;

namespace DRN.Test.Unit.Tests.Sample.Settings;

public class IdentityConfigTests
{
    [Fact]
    public void IdentityConfig_Should_Default_To_Confirmed_SignIn_Requirements()
    {
        var config = new IdentityConfig();

        config.RequireConfirmedAccount.Should().BeTrue();
        config.RequireConfirmedEmail.Should().BeTrue();
        config.RequireConfirmedPhoneNumber.Should().BeTrue();
    }
}
