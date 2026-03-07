using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DRN.Framework.Hosting.TagHelpers;

/// <summary>
/// Modern-defaults script tag helper. Renders a <c>&lt;script&gt;</c> element with:
/// <list type="bullet">
///   <item><description><c>defer</c> for external scripts (when <c>src</c> is set)</description></item>
///   <item><description><c>type="module"</c> for inline scripts (when <c>src</c> is not set)</description></item>
/// </list>
/// All defaults can be overridden explicitly.
/// </summary>
/// <example>
/// <code>
/// &lt;!-- External: renders with defer --&gt;
/// &lt;script src="buildwww/app/js/app.js" /&gt;
///
/// &lt;!-- Inline: renders with type="module" --&gt;
/// &lt;script&gt;console.log('strict + scoped');&lt;/script&gt;
///
/// &lt;!-- Opt-out: no defer --&gt;
/// &lt;script src="legacy.js" defer="false" /&gt;
///
/// &lt;!-- Opt-out: classic (non-module) inline --&gt;
/// &lt;script type="text/javascript"&gt;var x = 1;&lt;/script&gt;
/// </code>
/// </example>
[HtmlTargetElement("script")]
public class ScriptDefaultsTagHelper : TagHelper
{
    /// <summary>
    /// Optional script source URL. When set, the script is treated as external and <c>defer</c> is applied by default.
    /// </summary>
    [HtmlAttributeName("src")]
    public string? Src { get; set; }

    /// <summary>
    /// Controls the <c>defer</c> attribute for external scripts. Defaults to <c>true</c>.
    /// Set to <c>false</c> to suppress auto-defer (e.g., for scripts that must execute immediately).
    /// Ignored for inline scripts.
    /// </summary>
    public bool Defer { get; set; } = true;

    /// <summary>
    /// Explicit script type. When not set, inline scripts default to <c>"module"</c>.
    /// Set explicitly (e.g., <c>"text/javascript"</c>) to override the module default.
    /// </summary>
    public string? Type { get; set; }

    public override int Order => int.MaxValue;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var hasSrc = !string.IsNullOrEmpty(Src);
        if (hasSrc && Defer) // External script: auto-defer
        {
            output.Attributes.SetAttribute("defer", null);
            return;
        }

        // Inline script without explicit type → module (strict + scoped + deferred)
        var needsType = string.IsNullOrEmpty(Type);
        if (needsType && !hasSrc)
            output.Attributes.SetAttribute("type", "module");
    }
}