namespace Sample.Hosted.Pages.Shared.Models;

public class DefaultSubNavigationCollection() : SubNavigationCollection(DefaultItems)
{
    public static IReadOnlyList<SubNavigationItem> DefaultItems { get; } =
    [
        SubNavigationItem.ForPage(Get.Page.Root.Home, nameof(Get.Page.Root.Home), "bi-house-door"),
    ];
}

public class SubNavigationCollection(IReadOnlyList<SubNavigationItem> items, bool justifyContentCenter = false)
{
    public IReadOnlyList<SubNavigationItem> Items { get; } = items;
    public bool JustifyContentCenter { get; } = justifyContentCenter;
}

public class SubNavigationItem(string href, string title, string? icon = null)
{
    public static SubNavigationItem ForPage(string pageName, string title, string? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageName);
        return new(pageName, title, icon) { PageName = pageName };
    }

    public static SubNavigationItem ForUrl(string href, string title, string? icon = null) => new(href, title, icon);

    public string? PageName { get; private init; }
    public string Href { get; } = href;
    public string Title { get; } = title;
    public string? Icon { get; } = icon;

    public bool IsDefault { get; init; } = true;
    public int Order { get; init; } = 0;
}
