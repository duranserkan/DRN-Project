using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace DRN.Framework.Utils.Http;

/// <summary>
/// ExternalRequest request is a simple factory for your external http(s) request calls.
/// </summary>
public interface IExternalRequest
{
    IFlurlRequest For(Url endpoint, Version httpVersion);
}

[Singleton<IExternalRequest>]
public class ExternalRequest : IExternalRequest
{
    private static readonly DefaultJsonSerializer JsonSerializer = new(JsonConventions.DefaultOptions);
    private readonly IFlurlClient? _flurlClient;

    public ExternalRequest() : this(null)
    {
    }

    public ExternalRequest(HttpMessageHandler? httpMessageHandler)
    {
        _flurlClient = httpMessageHandler != null ? new FlurlClient(new HttpClient(httpMessageHandler, disposeHandler: false)) : null;
    }

    public IFlurlRequest For(Url endpoint, Version httpVersion) => For(endpoint, httpVersion.ToString());

    private IFlurlRequest For(Url endpoint, string httpVersion)
    {
        var flurlRequest = _flurlClient != null
            ? _flurlClient.Request(endpoint)
            : new FlurlRequest(endpoint);

        flurlRequest.WithSettings(x =>
        {
            x.HttpVersion = httpVersion;
            x.JsonSerializer = JsonSerializer;
        });

        flurlRequest.BeforeCall(x => x.HttpRequestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionExact);

        return flurlRequest;
    }
}