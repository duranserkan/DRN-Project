---
name: frontend-razor-pages-navigation
description: "Razor Pages navigation system - SidebarNavigationCollection for main navigation, SidebarSettingsCollection for user menu, SubNavigationCollection for tabs, and navigation data structures. Use for implementing and customizing application navigation UI. Keywords: razor-pages, navigation, sidebar, menu, sub-navigation, tabs, ui-components, navigation-collections, layout"
last-updated: 2026-10-04
difficulty: intermediate
tokens: ~1K
---

# Razor Page Navigation

> Standardized navigation data structures, sidebar configuration, and dynamic menu generation for hosted Razor Pages applications using this convention.

> [!NOTE]
> Use the repository profile to identify the hosted web project that acts as the local reference implementation.

## When to Apply
- Adding items to the Sidebar
- Creating actionable Sidebar items (badges, canvas)
- Managing navigation collections
- Configuring fixed bottom settings menu

## Navigation Collections

Navigation is driven by collection classes registered in Dependency Injection (DI) or passed via `ViewData`.

Use `ForPage(pageName, title, ...)` for Razor Page identifiers and `ForUrl(href, title, ...)` for URL entries. Shared views render page entries with `asp-page` so active state uses page identity. URL entries retain their href, including external URLs and fragments; Razor resolves explicit `~/` URL entries. Resolved root-relative href paths are compared with the public request path for active state. Existing constructors continue to create URL entries. Keep route accessors independent of deployment prefixes.

### SidebarNavigationCollection

Defines the main scrollable vertical sidebar items. This pattern allows for dynamic generation and ordering of sidebar elements.

```csharp
public class SidebarNavigationCollection(IReadOnlyList<SidebarNavigationItem> items, bool ordered = true)
{
    // Example default items list
    public static IReadOnlyList<SidebarNavigationItem> DefaultItems { get; } =
        new List<SidebarNavigationItem>
        {
            SidebarNavigationItem.ForPage(Get.Page.Root.Home, nameof(Get.Page.Root.Home), "bi-house-door"),
            new("#", "Dashboard", "bi-speedometer2"),
            // ...
        }.OrderBy(i => i.Order).ToArray();

    public IReadOnlyList<SidebarNavigationItem> Items { get; } = ordered ? items : items.OrderBy(x => x.Order).ToArray();
}
```

#### Item Configuration
Individual items are defined using a standardized class structure:

```csharp
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
```

### SidebarSettingsCollection

Defines the fixed bottom settings menu in the sidebar, typically used for user profile, settings, or logout actions.

```csharp
// Retrieved from ViewData in the view component or layout
var sidebarSettingsCollection = ViewData[Get.ViewDataKeys.SidebarSettingsCollection] as SidebarSettingsCollection 
                                ?? new SidebarSettingsCollection();
```

---

## SubNavigation

Secondary horizontal navigation (tabs) displayed above the main content area. This is configured via `MainContentLayoutOptions` in the Page Model.

```csharp
public class SubNavigationCollection(IReadOnlyList<SubNavigationItem> items, bool justifyContentCenter = false)
{
    public IReadOnlyList<SubNavigationItem> Items { get; } = items;
    public bool JustifyContentCenter { get; } = justifyContentCenter;
}
```

### Usage
In your Page Model or Controller:

```csharp
// In Controller/Page
ViewData[Get.ViewDataKeys.MainContentLayoutOptions] = new MainContentLayoutOptions 
{ 
    SubNavigation = new SubNavigationCollection([
        SubNavigationItem.ForPage(Get.Page.User.Profile.Details, "Profile", "bi-person"),
        SubNavigationItem.ForPage(Get.Page.User.Profile.Edit, "Edit", "bi-pencil-square")
    ])
};
```

### SubNavigationFor

Helper class pattern for defining reusable sub-navigation collections (e.g., accessed via `Get.SubNavigation`).

```csharp
public class SubNavigationFor
{
    public DefaultSubNavigationCollection DefaultSubNavigationCollection { get; } = new();
    public ProfileSubNavigationCollection ProfileSubNavigationCollection { get; } = new();
}
```

---

## Helper Classes & Components

### _Sidebar.cshtml (Component)
The Sidebar component renders the `SidebarNavigationCollection` as a scrollable list and `SidebarSettingsCollection` as a fixed bottom menu.

Key responsibilities include:
- **Slim UI Mode**: Adjusts icon spacing and hides text if a "Slim UI" claim/preference is active (e.g., `Get.Claim.Profile.SlimUi`).
- **Active State**: Highlights the current page. This can be handled via CSS/JS or server-side logic matching the current URL.
- **Tooltips**: Adds tooltips for collapsed/slim views to ensure usability.

```razor
@foreach (var navItem in sidebarNavigationCollection.Items)
{
    // ... rendering logic ...
    @if (navItem.PageName is { } pageName)
    {
        <a asp-page="@pageName" class="...">@navItem.Title</a>
    }
    else
    {
        <a href="@navItem.Href" class="...">@navItem.Title</a>
    }
}
```


---

## Related Skills
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout System
- [frontend-razor-accessors.md](../frontend-razor-accessors/SKILL.md) - Accessor patterns
