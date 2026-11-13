---
name: frontend-razor-pages-navigation
description: "Configure Razor sidebar menus, action panels, and tabs using application-owned navigation collections."
last-updated: 2026-11-13
difficulty: intermediate
---

# Razor Page Navigation

Use the profile to find the reference web project. These are application-owned conventions, not built-in ASP.NET types. Confirm its collection APIs before adapting the examples.

## When to Apply
- Adding items to the Sidebar
- Creating actionable Sidebar items (badges, canvas)
- Managing navigation collections
- Configuring fixed bottom settings menu

## Navigation Collections

Navigation is driven by collection classes registered in Dependency Injection (DI) or passed via `ViewData`.

Use `ForPage(pageName, title, ...)` for Razor Page identifiers and `ForUrl(href, title, ...)` for URLs. Page entries render with `asp-page` and use page identity for active state. URL entries retain external URLs and fragments. Razor resolves explicit `~/` entries. Compare resolved root-relative paths with the public request path for active state. Constructors also create URL entries. Keep accessors independent of deployment prefixes.

### SidebarNavigationCollection

Configure the main scrollable sidebar with page or URL entries:

```csharp
var navigation = new SidebarNavigationCollection([
    SidebarNavigationItem.ForPage(Get.Page.Root.Home, "Home", "bi-house-door"),
    SidebarNavigationItem.ForUrl("~/Reports", "Reports", "bi-graph-up", order: 10)
], ordered: false);
```

#### Item Configuration
Items expose title, icon, order, and page or URL identity. `IsDefault` defaults to true. `ordered: true` preserves the supplied order. Use `ordered: false` to sort by `Order`. Empty page names are invalid.

### SidebarActionCollection

Actionbar triggers and panels use the selected collection's `Items`. An item can set `TriggerPartialViewName` to reuse a custom trigger and `RenderCanvas = false` when its panel is owned by the layout. Both defaults preserve standard action rendering. Browser-owned badge and visibility state stays in JavaScript rather than `BadgeContentGenerator`, which runs during server rendering.

### SidebarSettingsCollection

Defines the fixed bottom settings menu in the sidebar, typically used for user profile, settings, or logout actions.

```csharp
// Retrieved from ViewData in the view component or layout
var sidebarSettingsCollection = ViewData[Get.ViewDataKeys.SidebarSettingsCollection] as SidebarSettingsCollection 
                                ?? new SidebarSettingsCollection();
```

---

## SubNavigation

Configure tabs above the main content through `MainContentLayoutOptions.SubNavigation`. `SubNavigationCollection` preserves supplied items and accepts `justifyContentCenter: true` for centered tabs.

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

Reuse shared tab collections through the local accessor, such as `Get.SubNavigation`. Keep page-specific configuration on the page model.

---

## Helper Classes & Components

### _Sidebar.cshtml (Component)
The Sidebar component renders the `SidebarNavigationCollection` as a scrollable list and `SidebarSettingsCollection` as a fixed bottom menu.

Preserve slim-mode spacing, active-state identity, and accessible labels or tooltips when text is hidden. Follow the [UI accessibility guidance](../basic-code-review/SKILL.md#ui-accessibility).


---

## Related Skills
- [frontend-razor-pages-shared.md](../frontend-razor-pages-shared/SKILL.md) - Layout System
- [frontend-razor-accessors.md](../frontend-razor-accessors/SKILL.md) - Accessor patterns
