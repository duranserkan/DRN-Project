using Flurl.Http;

namespace DRN.Framework.Utils.Extensions;

public static class FlurlExtensions
{
    public static int GetGatewayStatusCode(this FlurlHttpException exception) =>
        exception.StatusCode switch
        {
            >= 400 and < 500 => exception.StatusCode ?? 0,
            503 => 503,
            504 => 504,
            _ => 502
        };
}