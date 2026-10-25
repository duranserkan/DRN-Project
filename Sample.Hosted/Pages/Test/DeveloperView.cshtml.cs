using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Identity;

namespace Sample.Hosted.Pages.Test;

[Authorize(Policy = UserClaims.TestPagesAccess)]
public class DeveloperView(IExceptionUtils exceptionUtils, IExceptionPageContentProvider contentProvider) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var model = await exceptionUtils.CreateErrorPageModelAsync(HttpContext,
            new InvalidOperationException("Developer diagnostics preview."));
        var result = await contentProvider.CreateHtmlErrorContentResult(HttpContext, model);
        return Content(result.Content, result.ContentType);
    }
}
