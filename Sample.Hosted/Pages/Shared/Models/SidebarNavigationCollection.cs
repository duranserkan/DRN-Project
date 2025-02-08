namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarNavigationCollection(IReadOnlyList<SidebarNavigationItem> items, bool ordered = true)
{
    public static IReadOnlyList<SidebarNavigationItem> DefaultItems { get; } =
        new List<SidebarNavigationItem>
        {
            new(PageFor.Root.Home, nameof(PageFor.Root.Home), "bi-house-door"),
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
    public string Href { get; } = href;
    public string Title { get; } = title;
    public string Icon { get; } = icon;
    public int Order { get; } = order;
    
    public bool IsDefault { get; init; } = true;
}