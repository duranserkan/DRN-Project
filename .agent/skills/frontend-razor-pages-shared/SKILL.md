---
name: frontend-razor-pages-shared
description: "Razor Pages layout system - Layout hierarchy (_LayoutBase, _Layout), MainContentLayoutOptions for page configuration, HTMX integration for partial rendering, and Bootstrap grid integration. Foundation for consistent page structure and responsive layouts. Keywords: razor-pages, layout, html-shell, layout-options, htmx, partial-rendering, bootstrap, responsive-design, page-structure"
last-updated: 2026-11-04
difficulty: intermediate
tokens: ~1K
---

# Razor Pages Layout System

> Core layout system and shared Razor structure for hosted Razor Pages applications using this convention.

> [!NOTE]
> Use the repository profile to identify the hosted web project that acts as the local reference implementation.

## When to Apply
- Modifying `_Layout.cshtml` structure
- Changing global HTML structure (`_LayoutBase.cshtml`)
- Configuring page layout options (Title, Width, Card mode)
- Understanding HTMX layout integration

Follow the [UI accessibility rules](../basic-code-review/SKILL.md#ui-accessibility) when implementing controls.

## Contents

- [Directory Structure](#directory-structure)
- [Layout Hierarchy](#layout-hierarchy)
- [Page-Specific Scripts](#page-specific-scripts)
- [Layout Options](#layout-options)

## Directory Structure

A hosted Razor Pages application using this convention follows this structure in `Pages/Shared/`:

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

## Page-Specific Scripts

Load shared runtime and layout behaviors from the global shell. Reference page/area behavior bundles only from the owning page's `Scripts` section. Resolve concrete bundle names, commands, and paths through the repository profile and framework-specific frontend skills.

Every intermediate layout must forward the section, and both full-page and htmx base layouts must render it. Scripts needed after a partial navigation belong in the returned body, not only a head that the swap omits. Preserve configured nonce, integrity, application-prefix, and asset-resolution handling. Do not bypass those controls with a custom loader.

Keep component code in external behavior modules and server values in encoded data attributes. If serializing JSON, convert it to a string before attribute rendering so Razor encodes quotes. Keep only intentional inline-script fixtures and required early bootstrap configuration as explicit exceptions.

When extracting scripts, retain Razor namespace imports needed by remaining markup and configuration expressions. Resolve remaining symbols against their declarations and `_ViewImports.cshtml` before removing an import.

Registration must tolerate repeated script execution and late completion after the htmx load event. Scope state/listeners to mounted elements and release owned resources when elements leave. Browser cache reuse does not guarantee scripts execute once. Verify direct load, navigation into the page, revisit, and navigation away through source inspection or authorized runtime checks.

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
