using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AccessDeniedPageModel : PageModel
{
    internal const string RequestIdTempDataKey = "DRN.AccessDeniedRequestId";

    public string RequestId { get; private set; } = string.Empty;

    public void OnGet()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        RequestId = TempData[RequestIdTempDataKey] as string ?? HttpContext.TraceIdentifier;
    }
}
