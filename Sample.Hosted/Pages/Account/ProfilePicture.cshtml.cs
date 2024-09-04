using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Application.Services;

namespace Sample.Hosted.Pages.Account;

[Authorize]
[RequestSizeLimit(RequestSizeLimit)]
public class ProfilePictureModel(UserManager<IdentityUser> userManager, IProfilePictureService service) : PageModel
{
    private const long MaxFileSize = 100 * 1024; // 100KB size limit
    private const long RequestSizeLimit = MaxFileSize + 10 * 1024;

    [BindProperty] public ProfilePictureInput Input { get; set; } = null!;

    public string? ProfilePictureBase64 { get; set; }

    public async Task OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) throw ExceptionFor.NotFound("User not found");

        ProfilePictureBase64 = await service.GetProfilePictureAsBase64Async(user);
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

        return RedirectToPage("/Account/ProfilePicture");
    }
}

public class ProfilePictureInput
{
    [Required]
    [Display(Name = "Profile Picture")]
    [DataType(DataType.Upload)]
    public IFormFile? ProfilePicture { get; set; }

    [FileExtensions(Extensions = "jpg,jpeg", ErrorMessage = "Please upload a valid image file (jpg or jpeg).")]
    public string? FileName => ProfilePicture?.FileName;
}