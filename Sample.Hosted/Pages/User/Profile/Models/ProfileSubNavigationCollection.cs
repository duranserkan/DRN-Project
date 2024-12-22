using Sample.Hosted.Pages.Shared.Models;

namespace Sample.Hosted.Pages.User.Profile.Models;

public class ProfileSubNavigationCollection() : SubNavigationCollection(DefaultItems)
{
    public static IReadOnlyList<SubNavigationItem> DefaultItems { get; } =
    [
        new(PageFor.UserProfile.Details, nameof(PageFor.UserProfile.Details), "bi-file-earmark"),
        new(PageFor.UserProfile.Edit, nameof(PageFor.UserProfile.Edit), "bi-pencil-square"),
        new(PageFor.UserProfile.Picture, nameof(PageFor.UserProfile.Picture), "bi-image")
    ];
}