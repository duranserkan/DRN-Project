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
            _client.BaseAddress = new Uri(baseAddress);
        }
        
        return _client;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _handler?.Dispose();
    }
}