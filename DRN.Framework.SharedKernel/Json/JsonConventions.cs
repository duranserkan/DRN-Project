using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DRN.Framework.SharedKernel.Json;

public static class JsonConventions
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    static JsonConventions()
    {
        //https://stackoverflow.com/questions/58331479/how-to-globally-set-default-options-for-system-text-json-jsonserializer
        UpdateDefaultJsonSerializerOptions();
    }

    public static readonly JsonSerializerOptions DefaultOptions = SetJsonDefaults();

    private static void UpdateDefaultJsonSerializerOptions()
    {
        var fields = typeof(JsonSerializerOptions).GetFields(StaticPrivate)
            .Where(f => f.FieldType == typeof(JsonSerializerOptions)).ToArray();

        foreach (var optionField in fields)
            optionField.SetValue(null, DefaultOptions);
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
        options.Converters.Add(new Int64ToStringConverter());
        options.Converters.Add(new Int64NullableToStringConverter());
        options.AllowTrailingCommas = true;
        options.PropertyNameCaseInsensitive = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        options.MaxDepth = 32;
        options.TypeInfoResolver = JsonSerializer.IsReflectionEnabledByDefault
            ? new DefaultJsonTypeInfoResolver()
            : JsonTypeInfoResolver.Combine();

        return options;
    }

    public static JsonSerializerOptions SetHtmlSafeWebJsonDefaults(JsonSerializerOptions? options = null)
    {
        options = SetJsonDefaults(options);
        options.Encoder = JavaScriptEncoder.Default;

        return options;
    }
}