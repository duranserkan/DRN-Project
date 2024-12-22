using Sample.Hosted.Pages.User.Profile.Models;

namespace Sample.Hosted.Pages.Shared.Models;

public static class SubNavigationFor
{
    public static DefaultSubNavigationCollection DefaultSubNavigationCollection { get; } = new();
    public static ProfileSubNavigationCollection ProfileSubNavigationCollection { get; } = new();
}