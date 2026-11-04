---
name: frontend-razor-accessors
description: "Centralize Razor route identifiers, view-data keys, and presentation decisions without retaining request state. Keywords: razor-pages, navigation, routing, accessors, permissions, view-data"
last-updated: 2026-11-04
difficulty: intermediate
tokens: ~0.8K
---

# Razor Accessors

Follow local conventions. These application-owned examples need only ASP.NET Core; adapt identifiers to discovered pages, named endpoints, and policies. Centralized strings are not compiler-checked routes.

## Shared Identifiers

Group repeated identifiers, not request state:

```csharp
public static class Get
{
    public static ExamplePageFor Page { get; } = new();
    public static EndpointFor Endpoint { get; } = new();
    public static PolicyFor Policy { get; } = new();
    public static ViewDataKeys ViewDataKeys { get; } = new();
}

public class ExamplePageFor
{
    public UserPageFor User { get; } = new();
}

public class UserPageFor
{
    public string Login => "/User/Login";
}

public class EndpointFor
{
    public string QuestionDetails => "Questions.Details";
}

public class PolicyFor
{
    public string DeleteQuestions => "Questions.Delete";
}

public class ViewDataKeys
{
    public string Title => "Title";
}
```

## Request-Bound Usage

```razor
@using Microsoft.AspNetCore.Authorization
@inject IAuthorizationService Authorization
@{
    ViewData[Get.ViewDataKeys.Title] = "Questions";
    var canDelete = (await Authorization.AuthorizeAsync(
        User, resource: null, policyName: Get.Policy.DeleteQuestions)).Succeeded;
}
<a asp-page="@Get.Page.User.Login">Login</a>
<a asp-route="@Get.Endpoint.QuestionDetails" asp-route-id="42">Question 42</a>
@if (canDelete)
{
    <button type="button" data-action="delete-question">Delete</button>
}
```

The named endpoint and policy must be registered; the button's action wiring is application-owned. Match endpoint authorization, including the actual resource for resource-based policies. Visibility never replaces server enforcement.

Resolve URLs through tag helpers, MVC URL generation, request-aware `LinkGenerator`, or explicit `~/` paths. Distinguish page identifiers from URLs. Prefer profile-declared endpoint helpers for effective action metadata and generation failures. Never store request-specific URLs or authorization results statically.

## Named Presentation Decisions

Keep decisions on the view model or request-scoped service. For example, after resolving authorization:

```csharp
public record QuestionActions(bool Authorized, bool Locked)
{
    public bool CanDelete => Authorized && !Locked;
}
```

Use properties for cheap state checks and methods for arguments or generated content. Share `ViewData`/`TempData` keys between senders and receivers.

## Related Skills
- [frontend-razor-pages-navigation.md](../frontend-razor-pages-navigation/SKILL.md) - Navigation patterns
