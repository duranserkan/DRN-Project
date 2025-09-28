using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Serialization;

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
    public static readonly string DefaultValue = ConsentCookieValues.Default.Serialize();

    public ConsentCookie(string name, string? cookieString)
    {
        Name = name;
        ConsentString = (cookieString ?? string.Empty).DecodeAsString();
        if (string.IsNullOrEmpty(ConsentString))
            return;

        try
        {
            Values = ConsentString.Deserialize<ConsentCookieValues>() ?? new ConsentCookieValues();
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