using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Application.Services;
using Sample.Domain.Users;
using Sample.Hosted.Pages.Shared.Models;

namespace Sample.Hosted.Pages.User.Profile;

[Authorize]
[RequestSizeLimit(RequestSizeLimit)]
public class ProfilePictureModel(IProfilePictureService service,
    UserManager<SampleUser> userManager, SignInManager<SampleUser> signInManager) : PageModel
{
    private const long MaxFileSize = 100 * 1024; // 100KB size limit
    private const long RequestSizeLimit = MaxFileSize + 10 * 1024;

    [BindProperty] public ProfilePictureInput Input { get; set; } = null!;
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (Input.ProfilePicture == null) return Page();
        if (Input.ProfilePicture.Length > MaxFileSize)
        {
            ModelState.AddModelError(nameof(Input.ProfilePicture), "The uploaded file is too large. Maximum allowed size is 100KB.");
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null) throw ExceptionFor.NotFound("User not found");

        await using var stream = Input.ProfilePicture.OpenReadStream();
        await service.CreateProfilePictureAsync(user, stream, MaxFileSize);

        // Sign in the user to update the claims
        await signInManager.RefreshSignInAsync(user);

        TempData[TempDataFor.StatusMessage] = "Your profile picture has been updated";

        return RedirectToPage(PageFor.UserProfile.Picture);
    }
}

public class ProfilePictureInput
{
    [Required]
    [Display(Name = "Profile Picture")]
    [DataType(DataType.Upload)]
    public IFormFile? ProfilePicture { get; init; }

    [FileExtensions(Extensions = "jpg, jpeg", ErrorMessage = "Please upload a valid image file (jpg or jpeg).")]
    public string? FileName => ProfilePicture?.FileName;
}