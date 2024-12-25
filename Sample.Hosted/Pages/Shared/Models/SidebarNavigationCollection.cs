namespace Sample.Hosted.Pages.Shared.Models;

public class DefaultSidebarNavigationCollection() : SidebarNavigationCollection(DefaultItems)
{
    public static IReadOnlyList<SidebarNavigationItem> DefaultItems { get; } =
    [
        new(PageFor.Root.Home, nameof(PageFor.Root.Home), "bi-house-door"),
        new("#", "Dashboard", "bi-speedometer2"),
        new("#", "Orders", "bi-table"),
        new("#", "Products", "bi-grid"),
        new("#", "Customers", "bi-people-fill"),
        new("#", "Carts", "bi-cart3"),
        new("#", "Reports", "bi-graph-up"),
        new("#", "Integrations", "bi-puzzle"),
    ];
}

public class SidebarNavigationCollection(IReadOnlyList<SidebarNavigationItem> items)
{
    public IReadOnlyList<SidebarNavigationItem> Items { get; } = items;
}

public class SidebarNavigationItem(string href, string title, string icon)
{
    public string Href { get; } = href;
    public string Title { get; } = title;
    public string Icon { get; } = icon;

    public bool IsDefault { get; init; } = true;
    public int Order { get; init; } = 0;
}