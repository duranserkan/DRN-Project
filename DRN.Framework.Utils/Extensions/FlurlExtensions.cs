using System.Reflection;
using Flurl.Http;
using Flurl.Http.Testing;

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

    public static HttpTest ClearFilteredSetups(this HttpTest httpTest)
    {
        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fieldInfo = httpTest.GetType().GetField("_filteredSetups", bindingFlags)!;
        var setups = (List<FilteredHttpTestSetup>)fieldInfo.GetValue(httpTest)!;
        setups.Clear();

        return httpTest;
    }
}