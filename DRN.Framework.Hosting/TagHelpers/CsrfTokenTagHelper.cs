using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Json;
using DRN.Framework.Utils.Logging;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>
/// Tag helper that automatically adds CSRF token to Htmx requests.
/// This helper looks for the 'add-csrf-token' attribute and adds the token to hx-headers.
/// For HTMX requests other than GET and HEAD, it automatically adds the token even without the attribute.
/// Use 'disable-csrf' attribute to opt out of automatic CSRF token generation.
/// </summary>
[HtmlTargetElement(Attributes = "hx-post")]
[HtmlTargetElement(Attributes = "hx-put")]
[HtmlTargetElement(Attributes = "hx-delete,hx-patch")]
[HtmlTargetElement(Attributes = "hx-patch")]
[HtmlTargetElement(Attributes = "add-csrf-token")]
public class CsrfTokenTagHelper(IAntiforgery antiForgery, IHttpContextAccessor httpContextAccessor, IScopedLog scopedLog) : TagHelper
{
    private const string HxHeadersAttribute = "hx-headers";
    private const string CsrfTokenHeader = "RequestVerificationToken";
    private const string CsrfTokenPlaceHolder = "CSRF-TOKEN_Placeholder";
    private const string AddCsrfTokenAttribute = "add-csrf-token";
    private const string DisableCsrfAttribute = "disable-csrf-token";
    private const string AutoCsrfAttribute = "csrf-protection";
    private static readonly string[] HxMethodAttributes = ["hx-post", "hx-put", "hx-delete", "hx-patch"];

    private readonly IAntiforgery _antiForgery = antiForgery ?? throw new ArgumentNullException(nameof(antiForgery));
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
                var requestToken = _antiForgery.GetAndStoreTokens(httpContext);
                if (requestToken.RequestToken == null)
                {
                    scopedLog.AddWarning("Generated CSRF token is null. Using placeholder token.");
                    tokenValue = CsrfTokenPlaceHolder;
                }
                else
                {
                    tokenValue = requestToken.RequestToken;
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
        {
            // Remove the add-csrf-token attribute
            output.Attributes.RemoveAll(AddCsrfTokenAttribute);
        }
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
            return JsonSerializer.Deserialize<Dictionary<string, string>>(existingHeadersJson) ?? new();
        }
        catch (JsonException ex)
        {
            _ = ex;
            scopedLog.AddWarning($"Failed to parse existing hx-headers: {existingHeadersJson}");
            return new Dictionary<string, string>();
        }
    }

    private void UpdateExistingHeaders(TagHelperOutput output, Dictionary<string, string> headersDict, string requestToken)
    {
        headersDict[CsrfTokenHeader] = requestToken;
        var newJson = JsonSerializer.Serialize(headersDict);
        output.Attributes.SetAttribute(HxHeadersAttribute, newJson);
    }

    private void CreateNewHeaders(TagHelperOutput output, string existingHeadersJson, string requestToken)
    {
        Dictionary<string, string> headers;

        if (!string.IsNullOrEmpty(existingHeadersJson))
        {
            headers = new Dictionary<string, string>
            {
                ["existing"] = existingHeadersJson,
                [CsrfTokenHeader] = requestToken
            };
        }
        else
        {
            headers = new Dictionary<string, string>
            {
                [CsrfTokenHeader] = requestToken
            };
        }

        var newJson = JsonSerializer.Serialize(headers);
        output.Attributes.SetAttribute(HxHeadersAttribute, newJson);
    }
}