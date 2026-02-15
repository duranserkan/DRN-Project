---
name: frontend-razor-pages-shared
description: Razor Pages layout system - Layout hierarchy (_LayoutBase, _Layout), MainContentLayoutOptions for page configuration, HTMX integration for partial rendering, and Bootstrap grid integration. Foundation for consistent page structure and responsive layouts. Keywords: razor-pages, layout, html-shell, layout-options, htmx, partial-rendering, bootstrap, responsive-design, page-structure
last-updated: 2026-02-15
difficulty: intermediate
---

# Razor Pages Layout System

> Core layout system and shared Razor structure for DRN hosted applications.
> **Note**: `Sample.Hosted` serves as the reference implementation for these patterns.

## When to Apply
- Modifying `_Layout.cshtml` structure
- Changing global HTML structure (`_LayoutBase.cshtml`)
- Configuring page layout options (Title, Width, Card mode)
- Understanding HTMX layout integration

## Directory Structure

A standard DRN-hosted application follows this structure in `Pages/Shared/`:

```
Pages/Shared/
├── Models/
│   ├── MainContentLayoutOptions.cs
│   ├── SidebarNavigationCollection.cs
│   └── ViewDataKeys.cs
├── _Layout.cshtml             # Main layout (Sidebar + Content)
├── _LayoutBase.cshtml         # HTML Shell (Head, Scripts)
├── _LayoutBaseHtmx.cshtml     # Minimal shell for HTMX requests
├── _Footer.cshtml             # Global Footer
├── _Sidebar.cshtml            # Sidebar navigation
└── _SubNavigation.cshtml      # Sub-navigation tabs
```

## Layout Hierarchy

1. **_LayoutBase.cshtml**: The outer HTML shell. Loads CSS, JS, and defines `<html>`, `<head>`, `<body>`.
2. **_Layout.cshtml**: The application frame.
   - Responsible for rendering the Sidebar, SubNavigation, and main content wrappers.
   - Typically manages responsive behavior and authentication state visibility (e.g., showing menus only when MFA is completed).
   - Wraps the body in a container with a flexible flexbox layout.
   - Handles `TempData` status messages.
3. **Page Content**: Injected into `@RenderBody()` within the `main` tag.

### HTMX Integration
The system is designed to detect HTMX requests and serve partial content (`_LayoutBaseHtmx`) to avoid full page reloads, preserving the "Single Page App" feel while using server-side rendering.

```razor
// _Layout.cshtml
@{
    var isHtmxRequest = Context.Request.Headers["HX-Request"] == "true";
    Layout = isHtmxRequest ? "_LayoutBaseHtmx" : "_LayoutBase";
}
```

## Layout Options

Control how a page is rendered within the main content area using `MainContentLayoutOptions`. This pattern allows individual pages to dictate their container styling without duplicating layout code.

```csharp
public class MainContentLayoutOptions
{
    public string Title { get; set; } = string.Empty;
    public bool CenterVertically { get; set; }
    public bool CenterHorizontally { get; set; }

    public MainContentType Type { get; set; } = MainContentType.CardBody;
    public BootstrapColumnSize ColumnSize { get; set; } = BootstrapColumnSize.None;
    public BootstrapGridTier GridTier { get; set; } = BootstrapGridTier.Md;
    public BootstrapTextAlignment TextAlignment { get; set; } = BootstrapTextAlignment.TextStart;
    
    public SubNavigationCollection? SubNavigation { get; set; }
}
```

### MainContentType Enum
- **None**: Renders content directly.
- **Card**: Wraps content in a Bootstrap `.card`. Renders Title in `.card-header`.
- **CardBody**: Adds `.card-body` padding around content inside the card.

### Bootstrap Support
- **GridTier**: `Xs`, `Sm`, `Md`, `Lg`, `Xl`, `Xxl`.
- **ColumnSize**: `One` to `Twelve`, `Auto`, `None`.
- **Helper Extensions**: `CssColumnSize()`, `CssTextAlignment()` generate the appropriate Bootstrap classes.

### Usage
In your Page Model:

```csharp
public void OnGet()
{
    ViewData[Get.ViewDataKeys.MainContentLayoutOptions] = new MainContentLayoutOptions 
    {
        Title = "Login",
        Type = MainContentType.Card,
        CenterHorizontally = true,
        ColumnSize = BootstrapColumnSize.Six,
        GridTier = BootstrapGridTier.Md
    };
}
```

### LayoutOptionsFor

Helper factory pattern for creating common layout configurations (e.g., accessed via `Get.LayoutOptions`).

```csharp
public class LayoutOptionsFor
{
    public MainContentLayoutOptions Full(string title) => new() { Title = title };

    public MainContentLayoutOptions Centered(string title) => new() 
    { 
        Title = title, 
        CenterVertically = true,
        CenterHorizontally = true,
        ColumnSize = BootstrapColumnSize.Six 
    };
}
```

**Usage**:
```csharp
ViewData[Get.ViewDataKeys.MainContentLayoutOptions] = Get.LayoutOptions.Centered("Login");
```

---

## Related Skills
- [frontend-razor-pages-navigation.md](../frontend-razor-pages-navigation/SKILL.md) - Navigation & Sidebar
- [frontend-razor-accessors.md](../frontend-razor-accessors/SKILL.md) - Accessor patterns
