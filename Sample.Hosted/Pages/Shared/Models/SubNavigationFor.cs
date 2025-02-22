using Sample.Hosted.Pages.User.Profile.Models;

namespace Sample.Hosted.Pages.Shared.Models;

public class SubNavigationFor
{
    public DefaultSubNavigationCollection DefaultSubNavigationCollection { get; } = new();
    public ProfileSubNavigationCollection ProfileSubNavigationCollection { get; } = new();
}