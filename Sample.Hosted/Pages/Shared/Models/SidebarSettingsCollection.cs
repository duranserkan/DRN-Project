using Sample.Hosted.Helpers;

namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarSettingsCollection(IReadOnlyList<SidebarSettingsItem> items, bool ordered = true)
{
    public static IReadOnlyList<SidebarSettingsItem> DefaultItems { get; } = new List<SidebarSettingsItem>
    {
        new("New project..."),
        new("Advanced"),
        SidebarSettingsItem.ForPage(Get.Page.User.Profile.Details, "My Profile"),
        new(1),
        SidebarSettingsItem.ForPage(Get.Page.User.Logout, "Log out", 1)
    }.OrderBy(i => i.Order).ToArray();
    
    public SidebarSettingsCollection() : this(DefaultItems)
    {
    }
    
    public IReadOnlyList<SidebarSettingsItem> Items { get; } = ordered ? items : items.OrderBy(x => x.Order).ToArray();
}

public class SidebarSettingsItem
{
    public static SidebarSettingsItem ForPage(string pageName, string title, int order = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        return new(title, pageName, order) { PageName = pageName };
    }

    public static SidebarSettingsItem ForUrl(string href, string title, int order = 0) => new(title, href, order);

    public string? PageName { get; private init; }
    public SidebarSettingsItem(string title, string href = "#", int order = 0)
    {
        Title = title;
        Href = href;
        Order = order;
    }

    public SidebarSettingsItem(int order)
    {
        Title = string.Empty;
        Href = string.Empty;
        Divider = true;
        Order = order;
    }

    public string Href { get; }
    public string Title { get; }
    public bool Divider { get; }
    public int Order { get; } 
    
    public bool IsDefault { get; init; } = true;
}
