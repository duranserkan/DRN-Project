---
name: frontend-razor-pages-shared
description: "Configure Razor layout options, shared shells, and page scripts for full-page and HTMX navigation."
last-updated: 2026-11-13
difficulty: intermediate
---

# Razor Pages Layout System

Use the profile to find the reference web project. Confirm its application-owned layout names and options before adapting this convention.

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

1. `_LayoutBase.cshtml` supplies the HTML shell, styles, and scripts.
2. `_Layout.cshtml` supplies navigation, responsive content wrappers, authentication-dependent visibility, and `TempData` messages.
3. Page content renders through `@RenderBody()` inside `main`.

### HTMX Integration
Use `_LayoutBaseHtmx` for partial HTMX responses and `_LayoutBase` for full navigation. Treat the HTMX request header as a rendering preference, never as authorization.

## Page-Specific Scripts

Load shared runtime and layout behaviors from the global shell. Reference page/area behavior bundles only from the owning page's `Scripts` section. Resolve concrete bundle names, commands, and paths through the repository profile and framework-specific frontend skills.

Every intermediate layout must forward the section, and both full-page and htmx base layouts must render it. Scripts needed after a partial navigation belong in the returned body, not only a head that the swap omits. Preserve configured nonce, integrity, application-prefix, and asset-resolution handling. Do not bypass those controls with a custom loader.

Keep component code in external behavior modules and server values in encoded data attributes. If serializing JSON, convert it to a string before attribute rendering so Razor encodes quotes. Keep only intentional inline-script fixtures and required early bootstrap configuration as explicit exceptions.

When extracting scripts, retain Razor namespace imports needed by remaining markup and configuration expressions. Resolve remaining symbols against their declarations and `_ViewImports.cshtml` before removing an import.

Registration must tolerate repeated script execution and late completion after the htmx load event. Scope state/listeners to mounted elements and release owned resources when elements leave. Browser cache reuse does not guarantee scripts execute once. Verify direct load, navigation into the page, revisit, and navigation away through source inspection or authorized runtime checks.

## Layout Options

Use `MainContentLayoutOptions` to configure the title, centering, container, text alignment, and optional tabs. Defaults are an empty title, no centering, `CardBody`, `ColumnSize.None`, `GridTier.Md`, `TextStart`, and no sub-navigation.

### MainContentType Enum
- **None**: Renders content directly.
- **Card**: Wraps content in a Bootstrap `.card`. Renders Title in `.card-header`.
- **CardBody**: Adds `.card-body` padding around content inside the card.

### Bootstrap Support
- **GridTier**: `Xs`, `Sm`, `Md`, `Lg`, `Xl`, `Xxl`, `None`.
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

Reuse local factories such as `Get.LayoutOptions` for common configurations. Check their actual defaults before relying on them.

**Usage**:
```csharp
ViewData[Get.ViewDataKeys.MainContentLayoutOptions] = Get.LayoutOptions.Centered("Login");
```

---

## Related Skills
- [frontend-razor-pages-navigation.md](../frontend-razor-pages-navigation/SKILL.md) - Navigation & Sidebar
- [frontend-razor-accessors.md](../frontend-razor-accessors/SKILL.md) - Accessor patterns
