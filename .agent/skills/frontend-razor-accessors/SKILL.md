---
name: frontend-razor-accessors
description: "Centralize Razor route identifiers, view-data keys, and presentation decisions without retaining request state. Keywords: razor-pages, navigation, routing, accessors, permissions, view-data"
last-updated: 2026-11-13
difficulty: intermediate
---

# Razor Accessors

Follow local conventions. These application-owned examples need only ASP.NET Core. Adapt identifiers to discovered pages, named endpoints, and policies. Centralized strings are not compiler-checked routes.

## Shared Identifiers

Group repeated identifiers, not request state. A local `Get` accessor might expose:

| Accessor | Example value |
|---|---|
| `Get.Page.User.Login` | `/User/Login` page identifier |
| `Get.Endpoint.QuestionDetails` | `Questions.Details` endpoint name |
| `Get.Policy.DeleteQuestions` | `Questions.Delete` policy name |
| `Get.ViewDataKeys.Title` | `Title` view-data key |

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

Register the named endpoint and policy. The application owns the button's action wiring. Match endpoint authorization, including the actual resource for resource-based policies. Visibility never replaces server enforcement.

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
