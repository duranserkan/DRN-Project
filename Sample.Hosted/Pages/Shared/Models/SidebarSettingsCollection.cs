using Sample.Hosted.Helpers;

namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarSettingsCollection(IReadOnlyList<SidebarSettingsItem> items, bool ordered = true)
{
    public static IReadOnlyList<SidebarSettingsItem> DefaultItems { get; } = new List<SidebarSettingsItem>
    {
        new("New project..."),
        new("Advanced"),
        new("My Profile", Get.Page.User.Profile.Details),
        new(1),
        new("Log out", Get.Page.User.Logout, 1)
    }.OrderBy(i => i.Order).ToArray();
    
    public SidebarSettingsCollection() : this(DefaultItems)
    {
    }
    
    public IReadOnlyList<SidebarSettingsItem> Items { get; } = ordered ? items : items.OrderBy(x => x.Order).ToArray();
}

public class SidebarSettingsItem
{
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