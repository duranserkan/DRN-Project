---
name: frontend-razor-accessors
description: Type-safe static accessor pattern - 'Get' class for refactoring-safe routing (Get.Page, Get.Endpoint), permission checks (Get.Claim), and ViewData keys. Eliminates magic strings in Razor views and provides centralized navigation. Keywords: razor-pages, type-safety, navigation, routing, accessors, get-pattern, page-routing, endpoint-routing, view-helpers, refactoring-safety
---

# Static Accessor Pattern (The 'Get' Class)

> A pattern for providing type-safe, static access to routing, constants, and logic in Razor Views, avoiding "magic strings".

## When to Apply
- Creating a central entry point for application constants.
- Finding paths to pages or endpoints (`Get.Page...`).
- Checking permissions or feature flags in Views (`Get.Claim...`).
- defining strongly-typed keys for extensive dictionaries like `ViewData`.

---

## The `Get` Class

Host a central static class (typically named `Get`) in the `Hosted` project. This class acts as a directory for various helper collections.

```csharp
public static class Get
{
    // Navigation & Routing
    public static SamplePageFor Page { get; } = PageCollectionBase<SamplePageFor>.PageCollection;
    public static SampleEndpointFor Endpoint { get; } = (SampleEndpointFor)EndpointCollectionBase<SampleProgram>.EndpointCollection!;
    
    // Security (ScopeContext wrappers)
    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();
    
    // Constants
    public static ViewDataKeys ViewDataKeys { get; } = new();
}
```

---


---

## Helper Collections (Dependency Construction)

Collections are used to group accessors logicially (e.g., by feature or area).

**Page Collection Pattern**:
```csharp
public class SamplePageFor : PageCollectionBase<SamplePageFor>
{
    // Property injection for nested helpers
    public RootPageFor Root { get; } = new();
    public UserPageFor User { get; } = new();
}
```

**Endpoint Collection Pattern**:
```csharp
public class SampleEndpointFor : EndpointCollectionBase<SampleProgram>
{
    // Dependencies initialized in constructor/property
    public UserApiFor User { get; } = new();
    public QaApiFor QA { get; } = new();
}
```

---

## Helper Categories

### Type-Safe Routing (`PageFor` / `EndpointFor`)

Avoid hardcoding URL strings. Use helper classes that generate routes.

**Pattern**:
```csharp
// 1. Grouping Class (referenced by SampleEndpointFor)
public class UserApiFor
{
    public const string RouteTemplate = "Api/User/[controller]";
    
    // Intermediate grouping or direct controller endpoint
    public UserIdentityLoginFor LoginController { get; } = new();
}

// 2. Controller Endpoint Class
public class UserIdentityLoginFor : ControllerForBase<SampleIdentityLoginController>
{
    public UserIdentityLoginFor() : base(UserApiFor.RouteTemplate) { }

    // Returns type-safe endpoint reference
    public ApiEndpoint Login { get; private set; } = null!;
}
```

**Usage in Razor**:
```razor
<!-- Refactoring-safe link generation -->
<a href="@Get.Page.User.Login">Login</a>
<form hx-post="@Get.Endpoint.User.LoginController.Login.Path()">
```

### logical Accessors (`ClaimFor` / `FeatureFor`)

Wrap complex logic (like `ScopeContext` checks) into readable properties.

**Pattern**:
```csharp
public class ClaimFor
{
    // Encapsulate the "how"
    public bool CanDeleteQuestions => ScopeContext.HasClaim(Claims.DeleteQuestion);
}
```

**Usage**:
```razor
@if (Get.Claim.CanDeleteQuestions) 
{ 
    <button>Delete</button> 
}
```

### Dictionary Keys (`ViewDataKeys`)

If you use `ViewData` or `TempData`, define keys here to ensure sender and receiver use the same string.

```csharp
// Definition
public class ViewDataKeys
{
    public string Title => "Title";
}

// Usage
ViewData[Get.ViewDataKeys.Title] = "My Page";
```

---

## Related Skills
- [drn-hosting.md](../drn-hosting/SKILL.md) - Hosting base classes
- [frontend-razor-pages-navigation.md](../frontend-razor-pages-navigation/SKILL.md) - Navigation patterns
