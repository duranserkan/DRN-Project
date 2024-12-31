using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DRN.Framework.Hosting.Areas.Developer.Pages;

public class CompilationExceptionPage : PageModel
{
    public CompilationErrorModel ErrorModel { get; set; } = null!;
    
    public void OnGet()
    {
        
    }
}