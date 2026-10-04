using Sample.Hosted.Helpers;

namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarNavigationCollection(IReadOnlyList<SidebarNavigationItem> items, bool ordered = true)
{
    public static IReadOnlyList<SidebarNavigationItem> DefaultItems { get; } =
        new List<SidebarNavigationItem>
        {
            SidebarNavigationItem.ForPage(Get.Page.Root.Home, nameof(Get.Page.Root.Home), "bi-house-door"),
            new("#", "Dashboard", "bi-speedometer2"),
            new("#", "Orders", "bi-table"),
            new("#", "Products", "bi-grid"),
            new("#", "Customers", "bi-people-fill"),
            new("#", "Carts", "bi-cart3"),
            new("#", "Reports", "bi-graph-up"),
            new("#", "Integrations", "bi-puzzle"),
        }.OrderBy(i => i.Order).ToArray();

    public SidebarNavigationCollection() : this(DefaultItems)
    {
    }

    public IReadOnlyList<SidebarNavigationItem> Items { get; } = ordered ? items : items.OrderBy(x => x.Order).ToArray();
}

public class SidebarNavigationItem(string href, string title, string icon, int order = 0)
{
    public static SidebarNavigationItem ForPage(string pageName, string title, string icon, int order = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        return new(pageName, title, icon, order) { PageName = pageName };
    }

    public static SidebarNavigationItem ForUrl(string href, string title, string icon, int order = 0) =>
        new(href, title, icon, order);

    public string? PageName { get; private init; }
    public string Href { get; } = href;
    public string Title { get; } = title;
    public string Icon { get; } = icon;
    public int Order { get; } = order;
    
    public bool IsDefault { get; init; } = true;
}
