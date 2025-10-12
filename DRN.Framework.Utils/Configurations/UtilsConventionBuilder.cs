using DRN.Framework.SharedKernel.Json;
using Flurl.Http;
using Flurl.Http.Configuration;

namespace DRN.Framework.Utils.Configurations;

public static class UtilsConventionBuilder
{
    private static bool _triggered;
    private static readonly SemaphoreSlim StartupLock = new(1, 1);

    public static void BuildConvention()
    {
        if (_triggered) return;

        StartupLock.Wait();
        try
        {
            if (_triggered) return;
            BuildConfigurationConvention();
            _triggered = true;
        }
        finally
        {
            StartupLock.Release();
        }
    }

    private static void BuildConfigurationConvention()
    {
        _ = JsonConventions.DefaultOptions;
        FlurlHttp.Clients.Clear();
        FlurlHttp.Clients.WithDefaults(builder => builder.Settings.JsonSerializer = new DefaultJsonSerializer(JsonConventions.DefaultOptions));
    }
}