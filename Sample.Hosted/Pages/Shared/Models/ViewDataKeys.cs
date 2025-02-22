namespace Sample.Hosted.Pages.Shared.Models;

public class ViewDataKeys
{
    public string Title { get; } = nameof(Title);
    public string ReturnUrl { get; } = nameof(ReturnUrl);
    public string MainContentLayoutOptions { get; } = nameof(MainContentLayoutOptions);
    public string SidebarNavigationCollection { get; } = nameof(SidebarNavigationCollection);
    public string SidebarActionCollection { get; } = nameof(SidebarActionCollection);
    public string SidebarSettingsCollection { get; } = nameof(SidebarSettingsCollection);
}