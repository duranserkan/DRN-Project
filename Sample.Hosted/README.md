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

The sample enables [HTMX Problem Details](../DRN.Framework.Hosting/README.md#htmx-partial-requests). Failures preserve the current page and input and record one notification. Validation errors show a summary in the originating form or a `data-problem-region`, falling back to the page summary. Other failures show a brief toast with public guidance instead of a page banner. Generic HTTP titles such as "Bad Request" become plain-language messages. Request references remain available in history. A successful request clears its matching validation error. Dismissing history does not clear form validation. Network failures and timeouts show recovery guidance without automatically retrying a mutation.

Set `data-notification-operation="Save profile"` on a form or component for a history label. Set `data-notification-persist="false"` to keep its Problem Details and HTMX toast messages in memory only.

Fetch callers can use `DRN.Problem.fromResponse(response)` followed by `DRN.Problem.show(problem, context)`. Parsing leaves the original response body available. Context accepts `{ element, target, operation, persist, focus }`. Only `application/problem+json` is parsed, and the HTTP status takes precedence. Malformed, non-problem, or oversized responses use generic text. Limits are 65,536 response characters, 500 characters per display message, and 100 validation messages. Public messages render as text. See the [browser API declarations](buildwww/types/DrnProblem.d.ts) for all methods.

For an application-owned HTML validation fragment, return status `422`, `Content-Type: text/html`, and `X-DRN-Validation: true`. Only that combination permits a validation error swap. Other HTML errors and Problem Details JSON leave the target intact.

## Notification history

The Notifications action opens a searchable history panel with All, Errors, Warnings, and Info filters. Info includes success messages. Matching failures are grouped across retained history regardless of time gaps, with individual timestamps and request references preserved. Different operations, targets, severities, statuses, problem types, or message content remain separate. Use Load more to see additional history. Dismiss entries, groups, filtered results, or all history. Clear all offers Undo for 10 seconds. Closing a toast or the panel leaves history intact. Notifications are also available on login and pending-MFA pages.

The default `SidebarActionCollection` includes Notifications. Custom collections can omit or reorder it. The bell remains visible even when history is empty. Its badge counts unseen notifications and hides at zero. Opening the panel marks existing history as seen without dismissing it. Notifications arriving afterward remain unseen until the panel is opened again. Seen state survives reloads and Undo; repeated source identities do not become unseen again. Older cached records without seen state start unseen.

Success alerts also appear once in history without an extra toast. Closing an inline alert leaves history intact.

Actionbar items needing attention use Bootstrap `text-bg-warning` across the button. Opening an item's panel removes its warning highlight and restores the original button colors. Other action counts remain unchanged; opening Notifications also marks its history seen and clears the unseen badge. Count badges use Bootstrap `text-bg-danger`. Error severity remains visible inside history. Request references include a short instruction to share them with support so the request can be located.

Actionbar attendance is saved in `localStorage` per application and account. New tabs, reloads, and HTMX navigation keep an unchanged count acknowledged. Buttons render in their normal color until saved attendance is read, preventing an incorrect warning flash on acknowledged items. Already-open tabs receive attendance updates through storage events. A changed count restores attention. For capped counts such as `9+`, set `data-actionbar-attention-version` on the button to a stable activity revision that changes when new activity arrives. Without a revision, equal counts cannot distinguish new activity. If storage is blocked, attendance lasts only for the current page.

Acknowledgments of different action panels in overlapping tabs are retained independently. Existing saved attendance remains available after upgrading.

Toasts appear at the upper-right of the main content column, below the header and inside the sidebars. The `data-toast-host` wrapper anchors them outside the scrolling content. Bootstrap limits their width to the available space and opaque, theme-aware backgrounds keep text readable. Pages without a host use a bottom-center viewport fallback.

Toasts stay below offcanvas panels and modal backdrops, keeping Notifications and dialog controls unobstructed.

Toast display time and history retention are independent. `duration` controls only the toast, defaults to 5 seconds, and is clamped to 1 through 30 seconds. Timeout, the close button, eviction from the three visible toast slots, and HTMX cleanup leave history intact. History dismissal does not close a visible toast. Use `record: false` for transient feedback and `persist: false` for history that lasts only in memory.

### Choose the feedback channel

Use a toast when the user needs immediate feedback. Retain a notification when the user may need to revisit the result. Choose these independently. If neither is useful, avoid adding a message.

| Situation | Presentation and retention | API |
|---|---|---|
| Copied text or a routine preference change | Toast only | `DRN.Toast.show({ ..., record: false })` |
| Export completed or a background operation failed | Toast and history | `DRN.Toast.show({ ... })` |
| Background update that does not need immediate attention | History only | `DRN.Notifications.publish({ ... })` |
| Server-reported validation | Inline feedback and history | `DRN.Problem.show(problem, context)` or the automatic HTMX error flow |
| Request failure without validation errors | Brief toast and history with an unseen badge | `DRN.Problem.show(problem, context)` or the automatic HTMX error flow |

Prefer inline feedback for validation that shows where the user needs to act. Do not add a toast that repeats an inline result. The Problem flow records history once. Validation uses a toast only when no inline region is available; other request failures use a toast without adding a second history entry. Routine client-side field validation does not need a history entry.

`persist: false` is a separate storage choice. It keeps a retained notification in current-page memory rather than shared `localStorage`. It does not make the message toast-only and does not make sensitive content safe to display.

### Retention and privacy

History is limited to 1,000 records, 1 MiB of serialized UTF-8 data, and 24 hours. Old entries are removed first. `localStorage` shares saved history, seen state, and dismissals across tabs of the same origin and application. Web Locks serialize shared writes, merging each operation into the latest saved history so concurrent publications and dismissals cannot overwrite each other. Local feedback updates synchronously; persistence finishes asynchronously and may be lost if the page closes before its queued write runs. Browsers without Web Locks use tab-local `sessionStorage`. Storage or lock failures keep current-page feedback in memory. Receiving another tab's update does not replay its toast. Tab-only caches are not migrated into shared storage. Account changes clear history, Undo state, and contextual errors. A page that detects an account change in another tab pauses HTMX requests and notifications until reloaded. History is temporary and unsuitable for auditing. Keep secrets, request bodies, query values, and private diagnostics out of messages.

Authenticated users require a stable account ID. If it is missing or whitespace, the shell emits an empty notification owner, clears history, and suspends notifications and HTMX requests until a fresh account context is supplied. Request trace IDs are never used as account identities. Unauthenticated users retain the anonymous owner.

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
