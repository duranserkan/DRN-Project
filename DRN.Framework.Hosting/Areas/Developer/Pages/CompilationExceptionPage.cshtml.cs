using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DRN.Framework.Hosting.Areas.Developer.Pages;

public class CompilationExceptionPage : PageModel
{
    public CompilationErrorModel ErrorModel { get; set; } = null!;
    
    [SuppressMessage("Performance", "CA1822", Justification = "Razor Pages discovers OnGet as an instance handler.")]
    public void OnGet()
    {
        // ExceptionPageContentProvider supplies ErrorModel before rendering this page.
    }
}
