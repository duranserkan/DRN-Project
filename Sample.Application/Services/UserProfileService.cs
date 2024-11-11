using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;
using Sample.Domain.Users;
using Sample.Infra.Identity.Repositories;

namespace Sample.Application.Services;

public interface IUserProfileService
{
    Task<UserProfileEditModel> GetUserProfileEditModelAsync(ClaimsPrincipal principal);
    Task<UserProfileEditResult> UpdateUserAsync(UserProfileEditModel editModel, ClaimsPrincipal principal);
}

[Scoped<IUserProfileService>]
public class UserProfileService(UserManager<SampleUser> userManager, IUserProfileRepository repository) : IUserProfileService
{
    public async Task<UserProfileEditModel> GetUserProfileEditModelAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
            throw ExceptionFor.NotFound($"Unable to load user with Name '{userManager.GetUserId(principal)}'.");

        var result = new UserProfileEditModel
        {
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };

        return result;
    }

    public async Task<UserProfileEditResult> UpdateUserAsync(UserProfileEditModel editModel, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
            throw ExceptionFor.NotFound($"Unable to load user with ID '{userManager.GetUserId(principal)}'.");

        var profileModel = editModel.ToUserProfileModel();
        var result = await repository.UpdateUserProfileAsync(profileModel, user, principal);

        return result;
    }
}

public class UserProfileEditModel
{
    [Phone]
    [Display(Name = "Phone Number")]
    [Required]
    public string PhoneNumber { get; init; } = string.Empty;

    [Display(Name = "Slim UI")] public bool SlimUI { get; set; }

    public UserProfileModel ToUserProfileModel()
    {
        var userProfileModel = new UserProfileModel
        {
            PhoneNumber = PhoneNumber,
            SlimUI = SlimUI
        };

        return userProfileModel;
    }
}