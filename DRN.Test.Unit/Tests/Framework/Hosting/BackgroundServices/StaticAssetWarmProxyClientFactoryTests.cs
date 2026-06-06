using DRN.Framework.Hosting.BackgroundServices.StaticAssetWarm;

namespace DRN.Test.Unit.Tests.Framework.Hosting.BackgroundServices;

public class StaticAssetWarmProxyClientFactoryTests
{
    [Theory]
    [DataInlineUnit("http://localhost:5000")]
    [DataInlineUnit("https://127.0.0.1:5001")]
    [DataInlineUnit("https://[::1]:5001")]
    public void GetClient_Should_Allow_Loopback_BaseAddress(string baseAddress)
    {
        using var factory = new StaticAssetWarmProxyClientFactory();

        var client = factory.GetClient(baseAddress);

        client.BaseAddress.Should().NotBeNull();
        client.BaseAddress!.IsLoopback.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("https://example.com")]
    [DataInlineUnit("http://10.0.0.1:5000")]
    public void GetClient_Should_Reject_NonLoopback_BaseAddress(string baseAddress)
    {
        using var factory = new StaticAssetWarmProxyClientFactory();

        var act = () => factory.GetClient(baseAddress);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Static asset pre-warm requires a loopback base address:*");
    }
}
