using Microsoft.AspNetCore.Mvc.RazorPages;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;

namespace Sample.Hosted.Pages.Test;

[DeveloperExceptionPreview]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DeveloperView(IExceptionUtils exceptionUtils, IExceptionPageContentProvider contentProvider) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            // Throw the preview exception to capture stack frames for the diagnostics page.
            throw new InvalidOperationException("Developer diagnostics preview.");
        }
        catch (InvalidOperationException exception)
        {
            var model = await exceptionUtils.CreateErrorPageModelAsync(HttpContext, exception);
            var result = await contentProvider.CreateHtmlErrorContentResult(HttpContext, model);
            return Content(result.Content, result.ContentType);
        }
    }
}
