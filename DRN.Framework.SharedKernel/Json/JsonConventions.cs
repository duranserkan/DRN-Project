using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Json;

public static class JsonConventions
{
    private const string DefaultSerializerOptions = "s_defaultOptions";
    private const string DefaultWebSerializerOptions = "s_webOptions";
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    static JsonConventions()
    {
        //https://stackoverflow.com/questions/58331479/how-to-globally-set-default-options-for-system-text-json-jsonserializer
        UpdateDefaultJsonSerializerOptions();
    }

    public static readonly JsonSerializerOptions DefaultOptions = SetJsonDefaults();

    private static void UpdateDefaultJsonSerializerOptions()
    {
        var systemDefaults = typeof(JsonSerializerOptions).GetField(DefaultSerializerOptions, StaticPrivate)!;
        var webDefaults = typeof(JsonSerializerOptions).GetField(DefaultWebSerializerOptions, StaticPrivate)!;
        systemDefaults.SetValue(null, DefaultOptions);
        webDefaults.SetValue(null, DefaultOptions);
    }

    /// <summary>
    ///   <para>Option values appropriate to Web-based scenarios.</para>
    ///   <para>This member implies that:</para>
    ///   <para>- Property names are treated as case-insensitive.</para>
    ///   <para>- "camelCase" name formatting should be employed.</para>
    ///   <para>- Quoted numbers (JSON strings for number properties) are allowed.</para>
    /// </summary>
    public static JsonSerializerOptions SetJsonDefaults(JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        options.AllowTrailingCommas = true;
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

        return options;
    }
}