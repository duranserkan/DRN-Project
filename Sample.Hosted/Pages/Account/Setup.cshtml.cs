using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class SetupModel(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    SignInManager<IdentityUser> signInManager,
    IAppSettings appSettings) : PageModel
{
    [BindProperty] public SetupInput Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (appSettings.GetValue("InitialSetup:Completed", false) || (User.Identity?.IsAuthenticated ?? false))
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Check if an admin already exists
        var adminRoleExists = await roleManager.RoleExistsAsync("Admin");
        if (!adminRoleExists)
        {
            var role = new IdentityRole("Admin");
            await roleManager.CreateAsync(role);
        }

        var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Admin");
            await signInManager.SignInAsync(user, isPersistent: false);

            // Disable further access to this page
            appSettings.Configuration["InitialSetup:Completed"] = "true";

            return RedirectToPage("/Index");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}

public class SetupInput
{
    [Required] [EmailAddress] public string Email { get; init; } = null!;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = null!;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; init; } = null!;
}