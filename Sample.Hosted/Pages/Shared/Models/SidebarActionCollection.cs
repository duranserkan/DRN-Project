using DRN.Framework.SharedKernel.Enums;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Scope;

namespace Sample.Hosted.Pages.Shared.Models;

public class SidebarActionCollection
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

    public SidebarActionCollection(IReadOnlyList<SidebarActionItem> items)
    {
        var availableItems = ScopeContext.Settings.Environment == AppEnvironment.Development
            ? items
            : items.Where(item => !item.DevelopmentOnly);

        Items = availableItems.OrderBy(x => x.Order).ToArray();
    }

    public IReadOnlyList<SidebarActionItem> Items { get; }
}

public class SidebarActionItem
{
    public SidebarActionItem(string target, string title, string icon, int order = 0, string? partialViewName = null, bool developmentOnly = false)
    {
        Target = target;
        Title = title;
        Icon = icon;
        Order = order;
        DevelopmentOnly = developmentOnly;

        Id = Title.ToPascalCase();
        ActionItemId = $"offCanvasSidebarAction{Id}";
        ActionItemCanvasId = $"offCanvasSidebarActionCanvas{Id}";
        ActionItemCanvasLabelId = $"offCanvasSidebarActionLabel{Id}";
        ActionItemBadgeContentClass = $"offCanvasSidebarActionBadgeContent{Id}";
        PartialViewName = partialViewName ?? $"_SidebarActionbarItem{Title.ToPascalCase()}";
    }

    public string Id { get; }
    public string Target { get; }
    public string Title { get; }
    public string Icon { get; }
    public int Order { get; init; }
    public bool DevelopmentOnly { get; }

    public Func<string>? BadgeContentGenerator { get; init; }
    public string BadgeContent { get; init; } = string.Empty;
    public string BadgeVisuallyHiddenContent { get; init; } = string.Empty;

    public bool IsDefault { get; init; } = true;
    public string ActionItemId { get; }
    public string ActionItemCanvasId { get; }
    public string ActionItemCanvasLabelId { get; }
    public string ActionItemBadgeContentClass { get; }
    public string PartialViewName { get; }

    public string GetBadgeContent()
    {
        return BadgeContentGenerator?.Invoke() ?? BadgeContent;
    }
}