using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Net.Http.Json;

namespace DRN.Framework.SharedKernel.Conventions;

public static class JsonConventions
{
    private const string JsonHelpersFQN = "System.Net.Http.Json.JsonHelpers";
    private const string JsonHelpersOptions = "s_defaultSerializerOptions";
    private const string DefaultSerializerOptions = "s_defaultOptions";
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    static JsonConventions()
    {
        //https://stackoverflow.com/questions/58331479/how-to-globally-set-default-options-for-system-text-json-jsonserializer
        UpdateDefaultOptions();
        UpdateHttpClientJsonOptions();
    }

    public static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        AllowTrailingCommas = true
    };

    private static void UpdateDefaultOptions()
    {
        var systemDefaults = typeof(JsonSerializerOptions).GetField(DefaultSerializerOptions, StaticPrivate)!;
        systemDefaults.SetValue(null, DefaultOptions);
    }

    private static void UpdateHttpClientJsonOptions()
    {
        var helpers = typeof(JsonContent).Assembly.GetType(JsonHelpersFQN)!;
        var systemHttpJsonDefaultsField = helpers.GetField(JsonHelpersOptions, StaticPrivate)!;
        var systemHttpJsonDefaults = (JsonSerializerOptions)systemHttpJsonDefaultsField.GetValue(null)!;

        //update defaults one by one since  systemHttpJsonDefaultsField is static readonly
        systemHttpJsonDefaults.Converters.Add(new JsonStringEnumConverter());
        systemHttpJsonDefaults.AllowTrailingCommas = true;
    }
}