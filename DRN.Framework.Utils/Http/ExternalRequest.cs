using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace DRN.Framework.Utils.Http;

/// <summary>
/// ExternalRequest request is a simple factory for your external http(s) request calls.
/// <summary>
public interface IExternalRequest
{
    IFlurlRequest For(Url endpoint, Version httpVersion);
}

[Singleton<IExternalRequest>]
public class ExternalRequest : IExternalRequest
{
    private static readonly DefaultJsonSerializer JsonSerializer = new(JsonConventions.DefaultOptions);

    public IFlurlRequest For(Url endpoint, Version httpVersion) => For(endpoint, httpVersion.ToString());

    private static IFlurlRequest For(Url endpoint, string httpVersion)
    {
        return endpoint.WithSettings(x =>
        {
            x.HttpVersion = httpVersion;
            x.JsonSerializer = JsonSerializer;
        });
    }
}