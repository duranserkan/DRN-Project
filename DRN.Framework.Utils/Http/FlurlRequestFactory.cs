using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace DRN.Framework.Utils.Http;

internal static class FlurlRequestFactory
{
    public static IFlurlRequest Create(IFlurlClient? flurlClient, Url url, string httpVersion, DefaultJsonSerializer jsonSerializer)
    {
        var flurlRequest = flurlClient != null
            ? flurlClient.Request(url)
            : new FlurlRequest(url);

        flurlRequest.WithSettings(x =>
        {
            x.HttpVersion = httpVersion;
            x.JsonSerializer = jsonSerializer;
        });

        flurlRequest.BeforeCall(x => x.HttpRequestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionExact);

        return flurlRequest;
    }
}
