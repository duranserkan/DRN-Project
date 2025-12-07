using Sample.Hosted.Helpers;
using Sample.Hosted.Pages.Shared.Models;

namespace Sample.Hosted.Pages.User.Profile.Models;

public class ProfileSubNavigationCollection() : SubNavigationCollection(DefaultItems)
{
    public static IReadOnlyList<SubNavigationItem> DefaultItems { get; } =
    [
        new(Get.Page.User.Profile.Details, nameof(Get.Page.User.Profile.Details), "bi-file-earmark"),
        new(Get.Page.User.Profile.Edit, nameof(Get.Page.User.Profile.Edit), "bi-pencil-square"),
        new(Get.Page.User.Profile.Picture, nameof(Get.Page.User.Profile.Picture), "bi-image")
    ];
}