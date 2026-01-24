---
name: accessor-patterns
description: Static accessor pattern for type-safe view helpers and navigation
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
    public static SamplePageFor Page { get; } = new();
    public static SampleEndpointFor Endpoint { get; } = new();
    
    // Security (ScopeContext wrappers)
    public static RoleFor Role { get; } = new();
    public static ClaimFor Claim { get; } = new();
    
    // Constants
    public static ViewDataKeys ViewDataKeys { get; } = new();
}
```

---

## Helper Categories

### Type-Safe Routing (`PageFor` / `EndpointFor`)

Avoid hardcoding URL strings. Use helper classes that generate routes.

**Pattern**:
```csharp
public class UserApiFor
{
    // Returns type-safe endpoint reference
    public EndpointFor<LoginController> Login { get; } = new(c => c.Login);
}
```

**Usage in Razor**:
```razor
<!-- Refactoring-safe link generation -->
<a href="@Get.Page.User.Login">Login</a>
<form hx-post="@Get.Endpoint.QA.Question.PostAsync">
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
- [06-hosting.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/06-hosting.md) - Hosting base classes
- [15-razor-page-navigation.md](file:///Users/duranserkankilic/Work/Drn-Project/.agent/skills/15-razor-page-navigation/SKILL.md) - Navigation patterns
