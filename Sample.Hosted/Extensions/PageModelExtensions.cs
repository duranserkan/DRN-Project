using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;
using Sample.Hosted.Helpers;
using Sample.Hosted.Pages;

namespace Sample.Hosted.Extensions;

public static class PageModelExtensions
{
    public static async Task<IActionResult> RedirectToEnableAuthenticator(this PageModel pageModel, SignInManager<SampleUser> signInManager, SampleUser user)
    {
        await signInManager.SignInAsync(user, false, authenticationMethod: MfaClaimValues.MfaSetupRequired);

        return pageModel.RedirectToPage(Get.Page.UserManagement.EnableAuthenticator);
    }

    public static IActionResult ReturnPageWithUserRegisterErrors(this PageModel model, IdentityResult result)
    {
        foreach (var error in result.Errors)
            model.ModelState.AddModelError(error.Code, error.Description);

        return model.Page();
    }

    public static IActionResult ReturnLogoutPage(this PageModel pageModel) => pageModel.RedirectToPage(Get.Page.User.Logout);
}