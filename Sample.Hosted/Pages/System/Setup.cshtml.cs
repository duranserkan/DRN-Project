using Microsoft.AspNetCore.Identity;
using Sample.Domain.SampleIdentity;
using SampleUser = Sample.Domain.SampleIdentity.SampleUser;
using Sample.Hosted.Extensions;
using Sample.Hosted.Pages.User;

namespace Sample.Hosted.Pages.System;

[AllowAnonymous]
public class SetupModel(
    SignInManager<SampleUser> signInManager,
    IUserAdminRepository userAdminRepository) : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var adminUserExists = await userAdminRepository.AnySystemAdminExistsAsync();
        if (adminUserExists)
            return RedirectToPage(Get.Page.Root.Home);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var adminUserExists = await userAdminRepository.AnySystemAdminExistsAsync();
        if (adminUserExists)
            return RedirectToPage(Get.Page.Root.Home);

        var user = new SampleUser { UserName = Input.Email, Email = Input.Email };
        var result = await userAdminRepository.CreateSystemAdminForInitialSetup(user, Input.Password);
        if (result.Succeeded)
            return await this.RedirectToEnableAuthenticator(signInManager, user);

        return this.ReturnPageWithUserRegisterErrors(result);
    }
}
