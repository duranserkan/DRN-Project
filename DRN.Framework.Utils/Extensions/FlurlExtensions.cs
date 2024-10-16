using System.Net;
using System.Reflection;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
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

    public static async Task PrepareScopeLogForFlurlExceptionAsync(this FlurlHttpException ex, IScopedLog scopedLog, DrnAppFeatures appFeatures)
    {
        var call = ex.Call;
        var request = call.HttpRequestMessage;
        var requestVersion = request.Version;

        scopedLog.Add("FlurlExceptionHttpMethod", request.Method.ToString());
        scopedLog.Add("FlurlExceptionHttpVersionRequestUri", request.RequestUri?.ToString() ?? string.Empty);
        scopedLog.Add("FlurlExceptionHttpVersionPolicy", request.VersionPolicy.ToString());
        scopedLog.Add("FlurlExceptionHttpVersion", requestVersion.ToString());

        scopedLog.Add("FlurlExceptionCallCompleted", call.Completed);
        scopedLog.Add("FlurlExceptionCallStartedUtc", call.StartedUtc);
        if (call.EndedUtc != null)
            scopedLog.Add("FlurlExceptionCallEndedUtc", call.EndedUtc);
        if (call.Duration != null)
            scopedLog.Add("FlurlExceptionCallDuration", call.Duration);

        if (appFeatures.UseHttpRequestLogger && call.Response != null)
        {
            var response = await call.Response.GetStringAsync();
            scopedLog.Add("FlurlExceptionHttpResponse", response);
        }
    }

    public static HttpTest ClearFilteredSetups(this HttpTest httpTest)
    {
        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fieldInfo = httpTest.GetType().GetField("_filteredSetups", bindingFlags)!;
        var setups = (List<FilteredHttpTestSetup>)fieldInfo.GetValue(httpTest)!;
        setups.Clear();

        return httpTest;
    }
}