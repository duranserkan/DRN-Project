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
        
        scopedLog.Add("FlurlEx_Method", request.Method.ToString());
        scopedLog.Add("FlurlEx_Version", request.Version.ToString());
        scopedLog.AddIfNotNullOrEmpty("FlurlEx_RequestUri", request.RequestUri?.ToString() ?? string.Empty);
        scopedLog.AddIfNotNullOrEmpty("FlurlEx_VersionPolicy", request.VersionPolicy.ToString());

        scopedLog.Add("FlurlExCall_Completed", call.Completed);
        scopedLog.Add("FlurlExCall_Started", call.StartedUtc);
        if (call.EndedUtc != null)
            scopedLog.Add("FlurlExCall_Ended", call.EndedUtc);
        if (call.Duration != null)
            scopedLog.Add("FlurlExCall_Duration", call.Duration);

        if (call.Response != null)
        {
            var response = await call.Response.GetStringAsync();
            scopedLog.AddIfNotNullOrEmpty("FlurlExR_Response", response);
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