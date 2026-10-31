# Sample Application

This guide covers local startup, developer tools, error handling, notifications, and frontend builds for the [DRN-Project](../README.md) reference application.

## Contents

- [Run locally](#run-locally)
- [Swagger and test pages](#swagger-and-test-pages)
- [Error pages and API failures](#error-pages-and-api-failures)
- [Error handling conventions](#error-handling-conventions)
- [HTMX errors](#htmx-errors)
- [Notification history](#notification-history)
- [Frontend assets](#frontend-assets)
- [Frontend tests](#frontend-tests)

## Run locally

Install the [.NET SDK required by the repository](../docs/quick-start.md#prerequisites) and keep Docker running. From the repository root:

```bash
dotnet run --project Sample.Hosted -c Debug
```

The sample runs at [http://localhost:5998](http://localhost:5998). Its Debug/Development startup provisions local PostgreSQL through Testcontainers. See the [Docker and Docker Compose guide](../Docker/README.md) for dependency ports, container setup, credentials, data migration, and published image verification.

For framework setup and solution-wide build and test commands, see the [project quick start](../docs/quick-start.md).

## Swagger and test pages

Signed-in users who complete MFA can access `/swagger`, `/Test/Htmx`, and `/Test/DeveloperView` in Development. In Staging and Production, `SystemAdmin` grants access to Swagger and Test Pages. Other users need a `SwaggerAccess` claim set to `true` for Swagger or a `TestPagesAccess` claim set to `true` for Test Pages. MFA is required in every environment. Navigation uses the same policies as the endpoints.

Developer View is an explicit diagnostics preview. Ordinary Staging and Production errors remain redacted.

## Error pages and API failures

The sample provides anonymous, non-cacheable `/BadRequest` (400), `/AccessDenied` (403), `/NotFound` (404), and `/SomethingWentWrong` (500) pages. Staging and Production HTML server errors preserve their 5xx status and show only a request reference. HTML bad-request and not-found responses preserve 400 and 404. API failures for all 4xx and 5xx statuses use [Problem Details JSON](../DRN.Framework.Hosting/README.md#api-failure-responses) with a `requestId` in every environment. The sample configures `/Api` to identify unmatched API routes.

Each error page displays recovery guidance and a request reference for support. Cookie-authentication redirects to `/AccessDenied` preserve the denied request's reference for the next visit. Direct visits use the page request's reference. Browser authentication redirects to login for 401 responses.

For configuration and page requirements, see [Hosting custom error pages](../DRN.Framework.Hosting/README.md#custom-error-pages).

## Error handling conventions

Choose the response in the handler:

| Request and failure | Result convention | User experience |
|---|---|---|
| Eligible API failure | Existing Problem Details, typed problem, DRN exception, or empty error status | `application/problem+json` with the original HTTP status |
| HTMX operation failure | Explicit Problem Details or eligible empty error status; set safe defaults through `ScopeContext.Problem` when needed | Existing contextual presenter and one notification |
| HTMX field validation using JSON | Explicit `ObjectResult` containing `ValidationProblemDetails`, with status 400 or 422 | Exact field association, retained input, contextual summary |
| HTMX field validation using HTML | Application-owned partial following the [HTML validation contract](#htmx-errors) | Validation fragment replaces the target |
| Ordinary Razor form validation | Add ModelState errors and return `Page()` | Existing HTML fields and validation messages |
| Full Razor navigation failure | Existing empty status or exception handling | Configured HTML error page or Development diagnostics |

Preserve the failure's HTTP status. Use stable problem types such as `CategoryProblemTypes.NotFound`. Keep request-specific data out of type identifiers and private diagnostics out of public messages. See [Hosting API failure responses](../DRN.Framework.Hosting/README.md#api-failure-responses) for scoped defaults, request references, and endpoint opt-outs.

Field errors use the exact posted control name, including casing, property prefixes, and collection indices. Use `string.Empty` for form-level errors. Preserve existing API validation keys, including Identity error codes.

For an ordinary form handler:

```csharp
ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.ProfilePicture)}", "Please upload a valid JPEG image no larger than 100KB.");
return Page();
```

For a handler explicitly choosing the HTMX JSON validation contract, after adding safe ModelState errors:

```csharp
return new ObjectResult(new ValidationProblemDetails(ModelState)
{
    Status = StatusCodes.Status422UnprocessableEntity
});
```

Returning `Page()` with invalid ModelState remains HTML. Selecting JSON validation is an explicit handler choice.

## HTMX errors

The sample enables [HTMX Problem Details](../DRN.Framework.Hosting/README.md#htmx-partial-requests). Problem Details and failures without a usable toast header preserve the current page and input, show an error summary in the originating form or a `data-problem-region`, and record one notification. Errors without a local region use the page summary. A successful request clears its matching error. Dismissing history does not clear form validation. Network failures and timeouts show recovery guidance without automatically retrying a mutation.

Set `data-notification-operation="Save profile"` on a form or component for a history label. Set `data-notification-persist="false"` to keep its Problem Details and HTMX toast messages in memory only.

Fetch callers can use `DRN.Problem.fromResponse(response)` followed by `DRN.Problem.show(problem, context)`. Parsing leaves the original response body available. Context accepts `{ element, target, operation, persist, focus }`. Only `application/problem+json` is parsed, and the HTTP status takes precedence. Malformed, non-problem, or oversized responses use generic text. Limits are 65,536 response characters, 500 characters per display message, and 100 validation messages. Public messages render as text. See the [browser API declarations](buildwww/types/DrnProblem.d.ts) for all methods.

For an application-owned HTML validation fragment, return status `422`, `Content-Type: text/html`, and `X-DRN-Validation: true`. Only that combination permits a validation error swap. Other HTML errors and Problem Details JSON leave the target intact.

## Notification history

The Notifications action opens a searchable history panel with All, Errors, Warnings, and Info filters. Info includes success messages. Matching failures within 60 seconds are grouped with individual request references. Use Load more to see additional history. Dismiss entries, groups, filtered results, or all history. Clear all offers Undo for 10 seconds. Closing a toast or the panel leaves history intact. Notifications are also available on login and pending-MFA pages.

The default `SidebarActionCollection` includes Notifications. Custom collections can omit or reorder it. Its badge counts retained notifications and updates in the browser. The action hides when history is empty and the panel is closed.

Success alerts also appear once in history without an extra toast. Closing an inline alert leaves history intact.

Toast display time and history retention are independent. `duration` controls only the toast, defaults to 5 seconds, and is clamped to 1 through 30 seconds. Timeout, the close button, eviction from the three visible toast slots, and HTMX cleanup leave history intact. History dismissal does not close a visible toast. Use `record: false` for transient feedback and `persist: false` for history that lasts only in memory.

### Choose the feedback channel

Use a toast when the user needs immediate feedback. Retain a notification when the user may need to revisit the result. Choose these independently. If neither is useful, avoid adding a message.

| Situation | Presentation and retention | API |
|---|---|---|
| Copied text or a routine preference change | Toast only | `DRN.Toast.show({ ..., record: false })` |
| Export completed or a background operation failed | Toast and history | `DRN.Toast.show({ ... })` |
| Background update that does not need immediate attention | History only | `DRN.Notifications.publish({ ... })` |
| Server-reported validation or an actionable request failure | Inline feedback and history | `DRN.Problem.show(problem, context)` or the automatic HTMX error flow |

Prefer inline feedback when it shows where the user needs to act. Do not add a toast that repeats an inline result. The Problem flow records history once and uses a toast only when no inline region is available. Routine client-side field validation does not need a history entry.

`persist: false` is a separate storage choice. It keeps a retained notification in memory rather than `sessionStorage`. It does not make the message toast-only and does not make sensitive content safe to display.

### Retention and privacy

History is limited to 1,000 records, 1 MiB of serialized UTF-8 data, and 24 hours. Old entries are removed first. `sessionStorage` preserves history through current-tab reloads, with memory-only fallback when storage is unavailable. Account changes clear history, Undo state, and contextual errors. A page that detects an account change in another tab pauses HTMX requests and notifications until reloaded. History is temporary and unsuitable for auditing. Keep secrets, request bodies, query values, and private diagnostics out of messages.

### Publish notifications

```javascript
// A visible toast and one retained notification.
DRN.Toast.show({ type: 'success', message: 'Export completed.' });
// Routine feedback without history.
DRN.Toast.show({ type: 'info', message: 'Copied.', record: false });
// Retain only in memory when message content must not enter browser storage.
DRN.Toast.show({ type: 'warning', message: 'Review this operation.', persist: false });
// Record without showing a toast.
DRN.Notifications.publish({ type: 'info', title: 'Report ready', message: 'Open Reports to view it.' });
```

Server handlers can use `SendToast` for immediate `HX-Trigger: showToast` feedback. A Problem Details body takes precedence over a toast header. Other error responses display a nonempty string toast message when supplied, or a generic error otherwise. Use `DRN.Toast.show` for client feedback. Delayed `showToast` DOM events are unsupported. See the [notification API declarations](buildwww/types/DrnNotifications.d.ts) for available options and history operations.

## Frontend assets

Node.js and npm are required for rebuilding frontend assets. See [package.json](package.json) for the required Node.js version and build commands. Built assets are checked in, so this tooling is optional when simply running the sample.

After changing frontend sources in `Sample.Hosted/buildwww/`, rebuild the assets from `Sample.Hosted/`:

```bash
npm ci --ignore-scripts
npm audit signatures
npm run build
```

## Frontend tests

After installing and auditing dependencies as above, run frontend checks from `Sample.Hosted/`:

```bash
npm test
npm exec --no -- playwright install --only-shell chromium
npm run test:browser
```

The browser checks cover notification and form-error behavior without a running .NET app or database. On Linux, add `--with-deps` to the Playwright install command if system libraries are missing.
