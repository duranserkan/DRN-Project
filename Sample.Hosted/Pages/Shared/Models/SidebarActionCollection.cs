using DRN.Framework.Utils.Extensions;

namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarActionCollection(IReadOnlyList<SidebarActionItem> items, bool ordered = true)
{
    public static IReadOnlyList<SidebarActionItem> DefaultItems { get; } = new List<SidebarActionItem>
    {
        new("#", "Notifications", "bi-bell-fill") { BadgeContent = "2", BadgeVisuallyHiddenContent = "unread notifications" },
        new("#", "Messages", "bi-envelope") { BadgeContent = "9+", BadgeVisuallyHiddenContent = "unread messages" },
        new("#", "Support", "bi-headset") { BadgeContent = "1", BadgeVisuallyHiddenContent = "unread support responses" },
        new("#", "Documentation", "bi-layout-text-sidebar-reverse"),
        new("#", "Download", "bi-cloud-arrow-down"),
        new("#", "Upload", "bi-cloud-arrow-up"),
        new("#", "Service Status", "bi-heart-pulse")
    }.OrderBy(i => i.Order).ToArray();

    public SidebarActionCollection() : this(DefaultItems)
    {
    }

    public IReadOnlyList<SidebarActionItem> Items { get; } = ordered ? items : items.OrderBy(x => x.Order).ToArray();
}

public class SidebarActionItem(string target, string title, string icon, int order = 0)
{
    public string Id { get; } = title.ToPascalCase();
    public string Target { get; } = target;
    public string Title { get; } = title;
    public string Icon { get; } = icon;
    public int Order { get; init; } = order;

    public string BadgeContent { get; init; } = string.Empty;
    public string BadgeVisuallyHiddenContent { get; init; } = string.Empty;

    public bool IsDefault { get; init; } = true;
}