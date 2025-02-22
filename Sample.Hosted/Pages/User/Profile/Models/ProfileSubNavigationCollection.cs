using Sample.Hosted.Helpers;
using Sample.Hosted.Pages.Shared.Models;

namespace Sample.Hosted.Pages.User.Profile.Models;

public class ProfileSubNavigationCollection() : SubNavigationCollection(DefaultItems)
{
    public static IReadOnlyList<SubNavigationItem> DefaultItems { get; } =
    [
        new(Get.Page.UserProfile.Details, nameof(Get.Page.UserProfile.Details), "bi-file-earmark"),
        new(Get.Page.UserProfile.Edit, nameof(Get.Page.UserProfile.Edit), "bi-pencil-square"),
        new(Get.Page.UserProfile.Picture, nameof(Get.Page.UserProfile.Picture), "bi-image")
    ];
}