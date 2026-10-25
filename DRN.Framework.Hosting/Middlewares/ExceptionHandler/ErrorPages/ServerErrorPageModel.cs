using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ServerErrorPageModel : PageModel
{
    public string RequestId { get; set; } = string.Empty;

    public void OnGet()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        RequestId = HttpContext.TraceIdentifier;
    }
}
