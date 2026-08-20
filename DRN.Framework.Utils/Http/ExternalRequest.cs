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
public class ExternalRequest(HttpMessageHandler? httpMessageHandler) : IExternalRequest
{
    private static readonly DefaultJsonSerializer JsonSerializer = new(JsonConventions.DefaultOptions);
    private readonly IFlurlClient? _flurlClient = httpMessageHandler != null ? new FlurlClient(new HttpClient(httpMessageHandler, disposeHandler: false)) : null;

    public ExternalRequest() : this(null)
    {
    }

    public IFlurlRequest For(Url endpoint, Version httpVersion) => For(endpoint, httpVersion.ToString());

    private IFlurlRequest For(Url endpoint, string httpVersion) => FlurlRequestFactory.Create(_flurlClient, endpoint, httpVersion, JsonSerializer);
}
