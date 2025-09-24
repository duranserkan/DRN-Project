using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.Encodings;
using DRN.Framework.Utils.Extensions;

namespace DRN.Framework.Hosting.Consent;

public class ConsentCookieValues
{
    public static readonly ConsentCookieValues Default = new() { AnalyticsConsent = true, MarketingConsent = true };

    public bool? AnalyticsConsent { get; init; }
    public bool? MarketingConsent { get; init; }

    [JsonIgnore]
    public bool UserResponded => AnalyticsConsent.HasValue || MarketingConsent.HasValue;
}

public class ConsentCookie
{
    public static readonly string DefaultValue = JsonSerializer.Serialize(ConsentCookieValues.Default);

    public ConsentCookie(string name, string? cookieString)
    {
        Name = name;
        ConsentString = (cookieString ?? string.Empty).DecodeAsString(ByteEncoding.Base64UrlEncoded);
        if (string.IsNullOrEmpty(ConsentString))
            return;

        try
        {
            Values = JsonSerializer.Deserialize<ConsentCookieValues>(ConsentString) ?? new ConsentCookieValues();
        }
        catch (Exception e)
        {
            _ = e;
            Values = new ConsentCookieValues();
        }

        Values = JsonSerializer.Deserialize<ConsentCookieValues>(ConsentString) ?? new ConsentCookieValues();
        UserResponded = Values.UserResponded;
    }

    public string Name { get; }
    public string ConsentString { get; }

    public ConsentCookieValues Values { get; } = new();
    public bool UserResponded { get; }
}