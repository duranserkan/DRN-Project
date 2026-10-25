using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class AccessDenied : PageModel
{
    public string RequestId { get; private set; } = string.Empty;

    public void OnGet()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        RequestId = TempData[Get.TempDataKeys.AccessDeniedRequestId] as string ?? HttpContext.TraceIdentifier;
    }
}
