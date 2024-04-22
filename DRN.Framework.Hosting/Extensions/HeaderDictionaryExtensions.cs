using System.Text;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Extensions;

public static class HeaderDictionaryExtensions
{
    public static string ConvertToString(this IHeaderDictionary headerDictionary)
    {
        var stringBuilder = new StringBuilder(1024);

        var newLine = "";
        foreach (var pair in headerDictionary)
        {
            if (newLine == Environment.NewLine)
                stringBuilder.Append(newLine);
            else
                newLine = Environment.NewLine;

            stringBuilder.Append($"{pair.Key}: {pair.Value}");
        }

        return stringBuilder.ToString();
    }
}

public class DrnHttpHeaders(HttpContext httpContext)
{
    public string RequestHeaders { get; } = httpContext.Request.Headers.ConvertToString();
    public string ResponseHeaders { get; } = httpContext.Response.Headers.ConvertToString();
};