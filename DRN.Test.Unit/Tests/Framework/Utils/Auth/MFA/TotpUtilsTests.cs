using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth.MFA;

public class TotpUtilsTests
{
    // RFC 6238 Appendix B defines the HMAC-SHA1 test secret as the 20 US-ASCII bytes
    // "12345678901234567890". This is its unpadded RFC 4648 Base32 equivalent used by authenticator APIs.
    // References: https://www.rfc-editor.org/rfc/rfc6238.html#appendix-B
    //             https://www.rfc-editor.org/rfc/rfc4648.html#section-6
    private const string Rfc6238Sha1SharedKeyBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    // RFC 6238 Appendix B publishes these exact timestamps and eight-digit HMAC-SHA1 results
    // for a 30-second time step and Unix epoch T0.
    [Theory]
    [DataInlineUnit(59, "94287082")]
    [DataInlineUnit(1_111_111_109, "07081804")]
    [DataInlineUnit(1_111_111_111, "14050471")]
    [DataInlineUnit(1_234_567_890, "89005924")]
    [DataInlineUnit(2_000_000_000, "69279037")]
    [DataInlineUnit(20_000_000_000, "65353130")]
    public void TotpUtils_Should_Match_Rfc6238_Sha1_Test_Vectors(long unixTimeSeconds, string expected)
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);

        var code = TotpUtils.GenerateTotpCode(Rfc6238Sha1SharedKeyBase32, timestamp, digits: 8);

        code.Should().Be(expected);
        TotpUtils.VerifyTotpCode(Rfc6238Sha1SharedKeyBase32, expected, timestamp, allowedTimeStepDrift: 0, digits: 8).Should().BeTrue();
    }

    // This is a derived boundary test, not an Appendix B vector: 59 and 60 seconds fall in adjacent
    // 30-second counters, so drift 1 accepts the previous code while drift 0 rejects it.
    [Fact]
    public void TotpUtils_Should_Verify_Only_Within_Configured_TimeStep_Drift()
    {
        var generatedAt = DateTimeOffset.FromUnixTimeSeconds(59);
        var nextTimeStep = DateTimeOffset.FromUnixTimeSeconds(60);
        var code = TotpUtils.GenerateTotpCode(Rfc6238Sha1SharedKeyBase32, generatedAt, digits: 8);

        TotpUtils.VerifyTotpCode(Rfc6238Sha1SharedKeyBase32, code, nextTimeStep, allowedTimeStepDrift: 1, digits: 8).Should().BeTrue();
        TotpUtils.VerifyTotpCode(Rfc6238Sha1SharedKeyBase32, code, nextTimeStep, allowedTimeStepDrift: 0, digits: 8).Should().BeFalse();
    }
}
