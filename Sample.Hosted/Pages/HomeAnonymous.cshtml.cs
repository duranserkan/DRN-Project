using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Hosted.Helpers;

namespace Sample.Hosted.Pages;

[AllowAnonymous]
public class HomeAnonymous : PageModel
{
    public IActionResult OnGet()
    {
        if (ScopeContext.Authenticated)
            return RedirectToPage(Get.Page.Root.Home);

        return Page();
    }
}