using System.Text.Json;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>
/// Tag helper that automatically adds CSRF token to Htmx requests.
/// This helper looks for the 'add-csrf-token' attribute and adds the token to hx-headers.
/// For HTMX requests other than GET and HEAD, it automatically adds the token even without the attribute.
/// Use 'disable-csrf' attribute to opt out of automatic CSRF token generation.
/// </summary>
[HtmlTargetElement(Attributes = "hx-post")]
[HtmlTargetElement(Attributes = "hx-delete")]
[HtmlTargetElement(Attributes = "hx-patch")]
[HtmlTargetElement(Attributes = "hx-put")]
[HtmlTargetElement(Attributes = "add-csrf-token")]
public class CsrfTokenTagHelper(IHttpContextAccessor httpContextAccessor, IScopedLog scopedLog) : TagHelper
{
    private const string HxHeadersAttribute = "hx-headers";
    private const string CsrfTokenHeader = "RequestVerificationToken";
    private const string CsrfTokenPlaceHolder = "CSRF-TOKEN_Placeholder";
    private const string AddCsrfTokenAttribute = "add-csrf-token";
    private const string DisableCsrfAttribute = "disable-csrf-token";
    private const string AutoCsrfAttribute = "csrf-protection";
    private static readonly string[] HxMethodAttributes = ["hx-post", "hx-put", "hx-delete", "hx-patch"];

    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        var hasCsrfRequiringMethod = HxMethodAttributes.Any(attr => output.Attributes.ContainsName(attr));
        var hasExplicitCsrfAttribute = output.Attributes.ContainsName(AddCsrfTokenAttribute);
        var hasDisableCsrfAttribute = output.Attributes.ContainsName(DisableCsrfAttribute);

        // Return if CSRF is disabled
        if (hasDisableCsrfAttribute)
        {
            output.Attributes.SetAttribute(AutoCsrfAttribute, "disabled");
            // Remove the disable-csrf-token attribute
            output.Attributes.RemoveAll(DisableCsrfAttribute);
            return;
        }

        // Return if no CSRF requiring method and no explicit attribute
        if (!hasCsrfRequiringMethod && !hasExplicitCsrfAttribute)
            return;

        string tokenValue;
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                scopedLog.AddWarning("HttpContext is null. Using placeholder CSRF token.");
                tokenValue = CsrfTokenPlaceHolder;
            }
            else
            {
                tokenValue = httpContext.GetCsrfToken();
                if (string.IsNullOrEmpty(tokenValue))
                {
                    scopedLog.AddWarning("Generated CSRF token is empty. Using placeholder token.");
                    tokenValue = CsrfTokenPlaceHolder;
                }
            }
        }
        catch (Exception ex)
        {
            scopedLog.AddWarning("Error processing CSRF token. Using placeholder token.", ex);
            tokenValue = CsrfTokenPlaceHolder;
        }

        ProcessHxHeaders(output, tokenValue);

        // Add informative attribute about CSRF protection
        var protectionSource = hasExplicitCsrfAttribute ? "explicit" : "auto";
        output.Attributes.SetAttribute(AutoCsrfAttribute, protectionSource);

        if (hasExplicitCsrfAttribute)
            output.Attributes.RemoveAll(AddCsrfTokenAttribute); // Remove the add-csrf-token attribute
    }

    private void ProcessHxHeaders(TagHelperOutput output, string requestToken)
    {
        var existingHeadersJson = output.Attributes[HxHeadersAttribute]?.Value?.ToString() ?? string.Empty;
        var headersDict = DeserializeExistingHeaders(existingHeadersJson);

        if (headersDict.Keys.Count > 0)
            UpdateExistingHeaders(output, headersDict, requestToken);
        else
            CreateNewHeaders(output, existingHeadersJson, requestToken);
    }

    private Dictionary<string, string> DeserializeExistingHeaders(string existingHeadersJson)
    {
        if (string.IsNullOrEmpty(existingHeadersJson))
            return new Dictionary<string, string>();

        try
        {
            return existingHeadersJson.Deserialize<Dictionary<string, string>>() ?? new();
        }
        catch (JsonException ex)
        {
            _ = ex;
            scopedLog.AddWarning($"Failed to parse existing hx-headers: {existingHeadersJson}");
            return new Dictionary<string, string>();
        }
    }

    private static void UpdateExistingHeaders(TagHelperOutput output, Dictionary<string, string> headersDict, string requestToken)
    {
        headersDict[CsrfTokenHeader] = requestToken;
        var newJson = headersDict.Serialize();
        output.Attributes.SetAttribute(HxHeadersAttribute, newJson);
    }

    private static void CreateNewHeaders(TagHelperOutput output, string existingHeadersJson, string requestToken)
    {
        var headers = !string.IsNullOrEmpty(existingHeadersJson)
            ? new Dictionary<string, string>
            {
                ["existing"] = existingHeadersJson,
                [CsrfTokenHeader] = requestToken
            }
            : new Dictionary<string, string>
            {
                [CsrfTokenHeader] = requestToken
            };

        var newJson = headers.Serialize();
        output.Attributes.SetAttribute(HxHeadersAttribute, newJson);
    }
}

public static class CsrfExtensions
{
    public static string GetCsrfToken(this HttpContext context)
        => context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context).RequestToken ?? string.Empty;
}