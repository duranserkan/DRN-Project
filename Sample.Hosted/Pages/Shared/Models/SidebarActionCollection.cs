using DRN.Framework.Utils.Extensions;

namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarActionCollection() : SidebarActionCollectionBase(DefaultItems)
{
    public static IReadOnlyList<SidebarActionItem> DefaultItems { get; } =
    [
        new("#", "Notifications", "bi-bell-fill", "2", "unread notifications"),
        new("#", "Messages", "bi-envelope", "9+", "unread messages"),
        new("#", "Support", "bi-headset", "1", "unread support responses"),
        new("#", "Documentation", "bi-layout-text-sidebar-reverse"),
        new("#", "Download", "bi-cloud-arrow-down"),
        new("#", "Upload", "bi-cloud-arrow-up"),
        new("#", "Service Status", "bi-heart-pulse")
    ];
}

public abstract class SidebarActionCollectionBase(IReadOnlyList<SidebarActionItem> items)
{
    public IReadOnlyList<SidebarActionItem> Items { get; } = items;
}

public class SidebarActionItem(string target, string title, string icon, string? badgeContent = null, string? badgeVisuallyHiddenContent = null)
{
    public string Id { get; } = title.ToPascalCase();
    public string Target { get; } = target;
    public string Title { get; } = title;
    public string Icon { get; } = icon;


    public bool IsDefault { get; init; } = true;
    public int Order { get; init; } = 0;
    public string BadgeContent { get; init; } = badgeContent ?? string.Empty;
    public string BadgeVisuallyHiddenContent { get; init; } = badgeVisuallyHiddenContent ?? string.Empty;
}