using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public LoginModel(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [BindProperty] public LoginInput Input { get; set; }

    public string ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        if (User.Identity.IsAuthenticated)
        {
            RedirectToPage(PageFor.Home);
            return;
        }

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return string.IsNullOrWhiteSpace(returnUrl)
                ? RedirectToPage(PageFor.Home)
                : LocalRedirect(returnUrl);

        if (result.IsLockedOut)
            return RedirectToPage("./Lockout");

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");

        return Page();
    }
}

public class LoginInput
{
    [Required] [EmailAddress] public string Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Display(Name = "Remember me?")] public bool RememberMe { get; set; }
}