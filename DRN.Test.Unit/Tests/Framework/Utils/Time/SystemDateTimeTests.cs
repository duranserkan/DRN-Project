using DRN.Framework.Utils.Time;
using NSubstitute.Extensions;

namespace DRN.Test.Unit.Tests.Framework.Utils.Time;

public class SystemDateTimeTests
{
    [Theory]
    [DataInlineUnit(-2)]
    [DataInlineUnit(2)]
    public void ISystemDateTime_Should_Return_Disrupted_When_Enabled(int disruption, ISystemDateTimeProvider system)
    {
        var disrupt = false;
        var disruptedDateTime = DateTimeOffset.UtcNow;
        system.ReturnsForAll(callInfo => disrupt ? disruptedDateTime : DateTimeOffset.UtcNow);

        var now = system.UtcNow;
        now.Should().NotBe(disruptedDateTime);

        disrupt = true;
        now = system.UtcNow;
        now.Should().Be(disruptedDateTime);

        disruptedDateTime = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(disruption);
        now = system.UtcNow;
        now.Should().Be(disruptedDateTime);

        disrupt = false;
        now = system.UtcNow;
        now.Should().NotBe(disruptedDateTime);
    }
}