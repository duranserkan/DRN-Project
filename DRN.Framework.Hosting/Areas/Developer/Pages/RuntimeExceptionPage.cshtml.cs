using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DRN.Framework.Hosting.Areas.Developer.Pages;

public class RuntimeExceptionPage : PageModel
{
    public RuntimeExceptionPage()
    {
        
    }
    public static readonly string HtmlLineBreak = "</br>"+ Environment.NewLine;
    public const string LineBreak = @"\r\n";
    public const char LineBreak2 = '\r';
    public const char LineBreak3 = '\n';
    public static readonly char[] LineBreaks = [LineBreak2, LineBreak3];
    protected HtmlEncoder HtmlEncoder { get; set; } = HtmlEncoder.Default;
    public DrnExceptionModel ErrorModel { get; set; } = null!;

    public void OnGet()
    {
    }

    public string HtmlEncodeAndReplaceLineBreaks(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Split on the line breaks before passing it through the encoder.
        return string.Join(HtmlLineBreak, input.Split(LineBreak)
                .SelectMany(s => s.Split(LineBreaks, StringSplitOptions.None))
                .Select(HtmlEncoder.Encode));
    }
}