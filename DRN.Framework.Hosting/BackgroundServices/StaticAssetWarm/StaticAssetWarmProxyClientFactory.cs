using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetWarm;

public interface IStaticAssetWarmProxyClientFactory : IDisposable
{
    HttpClient GetClient(string baseAddress);
}

[Singleton<IStaticAssetWarmProxyClientFactory>]
public sealed class StaticAssetWarmProxyClientFactory : IStaticAssetWarmProxyClientFactory
{
    private HttpClientHandler? _handler;
    private HttpClient? _client;

    public HttpClient GetClient(string baseAddress)
    {
        var baseAddressUri = CreateLoopbackBaseAddress(baseAddress);

        if (_handler == null)
        {
            // Security: DangerousAcceptAnyServerCertificateValidator is scoped to this loopback-only
            // pre-warm handler and disposed after completion — never exposed to external requests.
            _handler = new HttpClientHandler();
            _handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        if (_client == null)
        {
            _client ??= new HttpClient(_handler);
            _client.BaseAddress = baseAddressUri;
        }

        return _client;
    }

    private static Uri CreateLoopbackBaseAddress(string baseAddress)
    {
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"Static asset pre-warm base address must be an absolute URI: {baseAddress}");

        if (!uri.IsLoopback)
            throw new InvalidOperationException($"Static asset pre-warm requires a loopback base address: {baseAddress}");

        return uri;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _handler?.Dispose();
    }
}
