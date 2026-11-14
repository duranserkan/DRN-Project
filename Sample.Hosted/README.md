# Sample Application

This guide covers local startup, developer tools, error handling, notifications, and frontend builds for the [DRN-Project](../README.md) reference application.

## Contents

- [Run locally](#run-locally)
- [Crawler policy and reverse proxies](#crawler-policy-and-reverse-proxies)
- [Swagger and test pages](#swagger-and-test-pages)
- [Pagination APIs](#pagination-apis)
- [Error pages and API failures](#error-pages-and-api-failures)
- [Error handling conventions](#error-handling-conventions)
- [HTMX errors](#htmx-errors)
- [Browser error reporting](#browser-error-reporting)
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

## Crawler policy and reverse proxies

[wwwroot/robots.txt](wwwroot/robots.txt) is served anonymously as a static file. It allows only `/User/Login$`. The `$` excludes longer paths and query-string variants. Robots rules guide crawlers, not authorization.

Hosting defaults the response to `Cache-Control: no-cache` so caches must revalidate the policy before reuse. Customize headers through [typed startup rules](../DRN.Framework.Hosting/README.md#static-asset-compression). Other static assets retain their existing cache lifetime.

For a public prefix such as `/app`, deploy the file with `Allow: /app/User/Login$`. Static files do not adapt to forwarded headers. Proxy the public origin's `/robots.txt` directly to the Sample service's `/robots.txt`, without a redirect or `/app` backend prefix. Return `200 OK` with `text/plain`.

[Robots rules apply to the entire origin](https://www.rfc-editor.org/rfc/rfc9309.html#section-2.3). When apps share a hostname, the gateway owns this single policy. This example blocks crawling of every other app too.

## Swagger and test pages

Signed-in users who complete MFA can access `/swagger`, `/Test/Htmx`, and `/Test/DeveloperView` in Development. In Staging and Production, `SystemAdmin` grants access to Swagger and Test Pages. Other users need a `SwaggerAccess` claim set to `true` for Swagger or a `TestPagesAccess` claim set to `true` for Test Pages. MFA is required in every environment. Navigation uses the same policies as the endpoints.

Developer View is an explicit diagnostics preview. Ordinary Staging and Production errors remain redacted.

`SampleDrnExceptionFilter` is an opt-in demonstration, registered explicitly by its integration test. It is not registered in the normal application, so its empty post-creation callback does not force diagnostic model collection for ordinary errors.

The HTMX test page has separate **Trigger server exception** and **Trigger browser exception** buttons. The server test sends a CSRF-protected HTMX POST and throws an unhandled exception, producing a toast and a history entry with a request reference. The browser test throws an uncaught JavaScript error, producing a generic error toast and one history entry with a client error reference. Open notification history to inspect each result. See [Browser error reporting](#browser-error-reporting) for reporting limits.

## Pagination APIs

Tag and Category use the shared SourceKnown controller contracts and Swagger guidance. Both require an Identity bearer token and completed MFA. Cookie-only authentication is not accepted. Razor Pages and htmx retain their CSRF protection.

Pagination responses contain `items` and `info`. The outer `info.firstId` and `info.lastId` identify the returned page. `info.request.pageCursor` describes the preceding page. Counts and page totals use `-1` when unspecified. Set `updateTotalCount=true` to refresh totals.

| Endpoint | Request and continuation |
|---|---|
| `GET /Api/QA/{Tag or Category}` | Flattened `PaginationRequest` query fields. Defaults to page 1 and size 10. The server fixes the ceiling at 100 and discards supplied `TotalCount` and `MarkAsHasNextOnRefresh`. |
| `GET /Api/QA/{Tag or Category}/Paginate` | Initial page using flat `PaginationOptions`: `pageSize`, `maxSize`, `updateTotalCount`, and `direction`. The server ceiling remains 100. |
| `POST /Api/QA/{Tag or Category}/Paginate` | Send a `PaginationRequest` as JSON for first-page, continuation or refresh requests. |

Use the returned `Info.RequestNextPage()`, `RequestPreviousPage()`, `RequestPage(page)` or `RequestRefresh()` to construct a continuation request. Send that request as JSON to `POST Paginate`, or flatten it into query fields for the collection GET. Jumps are limited to ten pages from the supplied cursor. Counts are recomputed only when requested, and refresh derives next-page availability from fetched data.

For non-.NET callers, set `pageNumber` to the desired page, `pageCursor.pageNumber` to the previous `info.request.pageNumber`, and both cursor IDs to the previous outer `info.firstId` and `info.lastId`. Preserve the page size and sort direction. Pagination failures use the API Problem Details contract below.

Migration: the former Tag `PaginateWithOptions`, `PaginateWithQuery`, and `PaginateWithBody` routes are removed. The collection GET now accepts `PaginationRequest`; `GET Paginate` accepts flat options. Stop posting `PaginationResultInfo` or supplying `jumpTo`. Creation returns HTTP 201 with a DTO and no Location header; retrieve it using `GET /Api/QA/{Tag or Category}/{dto.id}`. Repository validation rejects malformed Source-Known IDs and wrong entity types with HTTP 400. Valid missing IDs return HTTP 404, including for Category, which no longer supplies a custom not-found title. Tag retains DELETE; Category DELETE and both controllers' Lookup, Any and Count endpoints are disabled.

## Error pages and API failures

The sample provides anonymous, non-cacheable `/BadRequest` (400), `/AccessDenied` (403), `/NotFound` (404), and `/SomethingWentWrong` (500) pages. Staging and Production HTML server errors preserve their 5xx status and show only a request reference. HTML bad-request and not-found responses preserve 400 and 404. API failures for all 4xx and 5xx statuses use [Problem Details JSON](../DRN.Framework.Hosting/README.md#api-failure-responses) with a `requestId` in every environment. The sample configures `/Api` to identify unmatched API routes.

Each error page displays recovery guidance and a request reference for support. Cookie-authentication redirects to `/AccessDenied` preserve the denied request's reference for the next visit. Direct visits use the page request's reference. Browser authentication redirects to login for 401 responses.

For configuration and page requirements, see [Hosting custom error pages](../DRN.Framework.Hosting/README.md#custom-error-pages).

The anonymous `/User/Lockout` page uses the same error layout, with the configured lockout duration and links back to sign in or home. The duration is the full configured period, not an account-specific countdown.

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

Leave `Type` unset for routine errors. Hosting infers the standard type from the exception's status. Set a safe public title or detail only when useful:

```csharp
ScopeContext.Problem.Title = "Category not found";
ScopeContext.Problem.NotFound($"Category: {id}");
```

This throws `NotFoundException` and returns HTTP 404 with the standard type and the public title above. The diagnostic message stays private. Use explicit stable types only for conditions clients must distinguish beyond HTTP status. See [Hosting mappings and overrides](../DRN.Framework.Hosting/README.md#api-failure-responses).

Category lookup uses repository validation and standard Problem Details. Clients matching the former `urn:sample:problem:category-not-found` identifier or custom title should use HTTP 404 for a valid missing category and HTTP 400 for an invalid ID. The request reference remains available.

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

HTMX failure notifications are limited to requests triggered by trusted user events. Polling and load-triggered failures stay out of notification history. For a programmatically started request that belongs to a user action, set `data-notification-user-action="true"` on its element or containing component. Set it to `false` to suppress failure feedback even for trusted events. Connection failures and timeouts use a client error reference. Backend Problem Details retain their `requestId`. Both references can be searched and copied from notification details.

HTMX HTTP failures and server-sent toast headers use the existing Problem Details and notification paths. They do not submit another client-error report. HTMX connection failures and timeouts remain client-side failures and may submit a report under the [browser reporting limits](#browser-error-reporting).

Fetch callers can use `DRN.Problem.fromResponse(response)` followed by `DRN.Problem.show(problem, context)`. Parsing leaves the original response body available. Context accepts `{ element, target, operation, persist, focus }`. Only `application/problem+json` is parsed, and the HTTP status takes precedence. Malformed, non-problem, or oversized responses use generic text. Limits are 65,536 response characters, 500 characters per display message, and 100 validation messages. Public messages render as text. See the [browser API declarations](buildwww/types/drnProblem.types.ts) for all methods.

The Fetch reader consumes a clone incrementally and stops when the character limit or a 262,144-byte guard is exceeded, without trusting `Content-Length`. Cancelling the clone does not wait for the caller to consume the original. XHR parsing applies the character limit to its already-buffered response text.

For an application-owned HTML validation fragment, return status `422`, `Content-Type: text/html`, and `X-DRN-Validation: true`. Only that combination permits a validation error swap. Other HTML errors and Problem Details JSON leave the target intact.

## Browser error reporting

Expanded error details show the message, source, line and column, and page URL before the stack. Labels are bold. Values remain plain text, with stack indentation and line breaks preserved. The reporter reads the original stack once for both notification details and the server report. Local details use the first 500 UTF-16 code units of the message, keeping surrogate pairs intact. Action errors use `Action failed` when that prefix is blank. The display omits a duplicate stack heading based on the displayed message. The original stack remains unchanged for server reporting, subject to existing capture limits. If no browser frame lines are recognized, the stack section shows `Stack frames unavailable` while preserving other supplied stack text and source location. The reporter never generates a replacement stack. Existing saved details retain their original text order.

Browser URL sanitization uses `redacted` for matching path segments, query values, and named fragment values. Backend runtime does not match URL names. Explicit backend URL capture removes credentials and validates encoding; explicit value classifications use `[redacted]`.

Browser diagnostic arrays retain at most the first 256 positions and append a limit marker when truncated. Sparse holes become null and getters are not invoked.

For caught browser errors that prevent a user action, call the explicit action reporter:

```javascript
try {
    await saveProfile();
} catch (error) {
    DRN.ErrorHandler.reportActionError(error, {
        operation: 'Save profile',
        message: 'Could not save your profile. Check the current state before trying again.'
    });
}
```

The reporter creates one notification and a brief toast. Its `clientErrorId` is shared with any backend report and retained in the backend scoped log. This is a correlation reference, not a distributed trace ID. Provide only safe public text in `message` and `operation`. Select Show details to reveal client-side diagnostic messages, sources, line and column, page URLs, and stacks as selectable text. Select Hide details to collapse them again. These searchable `clientErrorDetails` retain the original browser values except for page URLs, which use diagnostic URL redaction before display or persistence. Details are limited to 4,000 characters. They follow the notification's account isolation, persistence, and retention limits. Reports sent to the server continue to use diagnostic capture with redaction. Action diagnostics show the message from an `Error` or the text of a thrown primitive, including `0` and `false`. Empty or whitespace-only messages appear as `Action failed`, as do other objects, functions, `null`, and `undefined`. Toasts keep the public message. `persist: false` keeps the notification in memory. Use `DRN.Problem.show` for backend Problem Details rather than reporting the same failure again as a browser error. Backend exception details are not included.

The server captures ClientErrorPayload using explicit annotations and capture budgets. It does not repeat browser name checks or apply configured runtime paths. Unclassified string fields, including Url, retain their values. Explicitly classified properties remain redacted. Browser data carries no server classification authority.

Action reporting returns `null` when notification services are unavailable or feedback fails, so a reporting error does not replace the original failure. Serialized reports are capped at 3,600 UTF-8 bytes, including JSON escaping, loss flags, and the client error reference. Diagnostics are sanitized before clipping. `diagnosticRedacted` and `diagnosticLimited` describe browser capture; the server independently enforces its policy.

`DRN.Diagnostics` imports the shared catalog at bundle time. Application JavaScript can call `DRN.Diagnostics.configure({ sensitiveFieldPaths: ['Checkout.Value'] })` to extend browser rules. Backend configuration is not sent automatically. Backend runtime uses explicit attributes or dictionary classifications instead of name rules. Free text remains bounded without content scanning. See the [diagnostic contract](../DRN.Framework.Utils/Logging/Diagnostics/README.md). Browser script changes require the normal frontend bundle build before deployment.

Apply the [application responsibilities for dynamic data](../DRN.Framework.Utils/Logging/Diagnostics/README.md) to browser reports as well.

Unhandled global errors and promise rejections show a generic error toast and one notification history entry when notifications are available and active. The toast uses `record: false` to avoid duplicate history. They use a `clientErrorId` shared with any backend report. Browser diagnostic details appear inside the expanded notification occurrence, while the toast and notification summary stay generic. The browser's default error reporting is preserved. Console calls are not forwarded.

Startup feedback captured before notification initialization is retained when the first valid account context is configured. It keeps its original timestamp and client error reference and merges with saved history. An invalid initial owner or prior suspension discards this feedback. Later account changes never carry it into another account.

Reports remain best effort, with a shared browser limit of five reports per minute. Global reports, toasts, and history entries also use ten-second duplicate suppression; throttled global failures create none. Separate explicit action failures keep separate references and local feedback even when reporting is throttled.

The browser sends diagnostics only when the rendered notification context identifies a signed-in account and the store is not suspended. Reports before that context is available are skipped. A reference may have no backend report when offline, unauthenticated, or throttled. Reporting failures do not prevent local feedback. Suspended accounts receive no new history entries.

The endpoint independently requires authentication. `ClientErrorRateLimitRule` composes with [DRN's rate limiting](../DRN.Framework.Hosting/README.md#rate-limiting) to apply a per-user sliding-window limit of five requests per minute per application instance, with no queue and HTTP 429 on rejection. Keep framework rate limiting enabled for this server limit to apply.

## Notification history

Severity filters use primary for All, danger for Errors, warning for Warnings, and info for Info. Filters with entries have a colored outline. The selected filter has a matching fill, even when empty. Empty, unselected filters stay neutral. Counts and `aria-pressed` preserve availability and selection information independently of color.

The Notifications action opens a searchable history panel with All, Errors, Warnings, and Info filters. Info includes success messages. Matching failures are grouped across retained history regardless of time gaps, with individual timestamps and request references preserved. Different operations, targets, severities, statuses, problem types, or message content remain separate. Use Load more to see additional history. Dismiss entries, groups, filtered results, or all history. Clear all offers Undo for 10 seconds. Closing a toast or the panel leaves history intact. The actionbar and notification bell are shown only after MFA completion.

The default `SidebarActionCollection` includes Notifications. Custom collections can omit or reorder it. The bell remains visible even when history is empty. Its badge counts unseen notifications and hides at zero. Opening the panel marks existing history as seen without dismissing it. Notifications arriving afterward remain unseen until the panel is opened again. Seen state survives reloads and Undo; repeated source identities do not become unseen again. Older cached records without seen state start unseen.

The badge updates immediately while the closed panel defers filtering, grouping, and list rendering until it opens. History reads avoid repeated sorting and entry serialization until records change or expire.

Success alerts also appear once in history without an extra toast. Closing an inline alert leaves history intact.

Actionbar items needing attention use Bootstrap `text-bg-warning` across the button. Opening an item's panel removes its warning highlight and restores the original button colors. Other action counts remain unchanged; opening Notifications also marks its history seen and clears the unseen badge. Count badges use Bootstrap `text-bg-danger`. Error severity remains visible inside history. Request references include a short instruction to share them with support so the request can be located.

Actionbar attendance is saved in `localStorage` per application and account. New tabs, reloads, and HTMX navigation keep an unchanged count acknowledged. Buttons render in their normal color until saved attendance is read, preventing an incorrect warning flash on acknowledged items. Already-open tabs receive attendance updates through storage events. A changed count restores attention. For capped counts such as `9+`, set `data-actionbar-attention-version` on the button to a stable activity revision that changes when new activity arrives. Without a revision, equal counts cannot distinguish new activity. If storage is blocked, attendance lasts only for the current page.

Acknowledgments of different action panels in overlapping tabs are retained independently. Existing saved attendance remains available after upgrading.

Toasts appear at the upper-right of the main content column, below the header and inside the sidebars. The `data-toast-host` wrapper anchors them outside the scrolling content. Bootstrap limits their width to the available space and opaque, theme-aware backgrounds keep text readable. Pages without a host use a bottom-center viewport fallback.

Toasts stay below offcanvas panels and modal backdrops, keeping Notifications and dialog controls unobstructed.

Toast display time and history retention are independent. At most three toasts are visible at once. A shared per-page budget admits at most five new toasts in any ten seconds; excess presentation is dropped without queuing or discarding retained history. `duration` controls only the toast, defaults to 5 seconds, and is clamped to 1 through 30 seconds. Timeout, the close button, eviction from the three visible toast slots, and HTMX cleanup leave history intact. History dismissal does not close a visible toast. Use `record: false` for transient feedback and `persist: false` for history that lasts only in memory.

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

Synchronous bursts share one queued Web Lock operation and storage write. Their deltas still apply in order, including dismissal followed by Undo. Unchanged history does not trigger another storage write. Web Storage access remains synchronous inside the lock.

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

Server handlers can use `SendToast` for immediate `HX-Trigger: showToast` feedback. A Problem Details body takes precedence over a toast header. Other error responses display a nonempty string toast message when supplied, or a generic error otherwise. Use `DRN.Toast.show` for client feedback. Delayed `showToast` DOM events are unsupported. See the [notification API declarations](buildwww/types/drnNotifications.types.ts) for available options and history operations.

## Frontend assets

The Sample frontend targets recent browsers with native ES2025 support.

To rebuild frontend assets, use npm and the exact Node.js version pinned in [`.node-version`](../.node-version). The install command below and CI both disable install scripts, enforce the engine requirements in [package.json](package.json), and use the public npm registry. Use these flags on a fresh clone. Built assets are checked in, so this tooling is optional when simply running the sample.

After changing frontend sources in `Sample.Hosted/buildwww/`, rebuild the assets from `Sample.Hosted/`:

```bash
npm ci --ignore-scripts --engine-strict --registry=https://registry.npmjs.org/
npm audit signatures --registry=https://registry.npmjs.org/
npm run build
```

Before generating assets, `npm run build` runs `npm run check`. Oxfmt checks the frontend sources and Vite and Playwright configs. Type-aware Oxlint fails on warnings, errors, and unused disable directives. Stylelint checks all CSS and SCSS under `buildwww/` and fails on warnings or errors. Run it separately with `npm run lint:styles`. TypeScript then checks the application and test projects. Use `npm run format` to apply formatting or `npm run format:check` to check it. Targeted builds skip these checks. See the [Oxlint rules](.oxlintrc.json) and [Stylelint rules](.stylelintrc.json).

`npm run check` ends with an advisory Knip report on unused files, dependencies, and exports. The full build prints this report in CI. Run `npm run check:unused` directly to get a failing exit code for findings. The script sets `BUILD_TYPE=app` so Knip discovers that variant’s inputs from Vite. Keep the other variants’ bundle entries in [knip.json](knip.json). Review findings against Razor templates and browser globals before removing code.

Build scripts use inline environment assignments such as `BUILD_TYPE=app vite build` and require a POSIX-compatible shell unless the scripts are made cross-platform. Vite selects each target through `BUILD_TYPE`, which targeted npm build scripts set explicitly. Direct Vite calls also use `BUILD_TYPE` and default to `app` when it is unset or empty.

### HTMX navigation

Same-origin swaps keep scripts only when their nonce matches the nonempty `HX-Nonce` response header, if supplied, and every CSP policy that restricts scripts. Each policy uses the first directive it defines from `script-src-elem`, `script-src`, and `default-src`, in that order. Without a header nonce, CSP must supply one. Cross-origin swaps strip all scripts. These rules also cover nested templates. If filtering fails, HTMX receives empty content.

Swapped markup cannot disable `safe-nonce` through `hx-ext` or `data-hx-ext`.

Fragment links such as `#section` preserve unsaved input. Back or Forward to a boosted history entry reloads the page and can discard a draft.

### Independently updating component behaviors

Full and targeted Vite builds record the Base64 SHA-256 hash of each manifest asset's uncompressed bytes. They emit a Brotli quality 11 `.br` file when it is smaller. Entries listed in `precompressAssets` must be requested through the Razor tag helpers. The build omits their originals when no bundled output references the original URL and marks them `brotliOnly` in the manifest. Keep entries with external or dynamically constructed original URLs off that list. Deploy the emitted files and manifest together. Razor links directly to the available Brotli file under the current proxy prefix.

Application TypeScript lives in `buildwww/app/ts/`. HTMX and Bootstrap adapters stay under `buildwww/lib/`. Vite emits browser JavaScript under `wwwroot/`.

RSJS component modules live in `buildwww/app/ts/behaviors/`, grouped by UI area. Each area has a separate entry, hashed output, and manifest:

| Entry | Modules | Loaded by | Output directory |
|---|---|---|---|
| `layoutBehaviors.ts` | `behaviors/layout/`: footer and tooltips | Shared full-page layout | `wwwroot/behaviors/layout/` |
| `homeBehaviors.ts` | `behaviors/home/`: HelloReact island | `HomeAnonymous` page's `Scripts` section | `wwwroot/behaviors/home/` |
| `testBehaviors.ts` | `behaviors/test/`: browser exception demo | `Test/Htmx` page's `Scripts` section | `wwwroot/behaviors/test/` |

The layout bundle loads after the shared runtimes. Page sections are forwarded through both full-page and HTMX layouts, so navigating to a page loads its entry when needed. Other pages do not reference the home or test entries. Shared runtimes, including React, remain global. Preload provides `DRN.React.mount`, which returns `null` until the React bundle loads. An onmount callback can return `false` to retry on the next poll. Cached scripts can execute again during HTMX navigation. `DRN.Onmount` keeps registration idempotent and owns per-element cleanup. Page bundles must not install unguarded document-level listeners. Browser caching does not mean a script executes only once.

The HTMX bundle installs `DRN.Validation`. To register a custom validation provider, call its `addProvider` method after that bundle loads and before validation bootstraps.

Pass component configuration through Razor-encoded data attributes. Register one behavior per source module and import only the area's modules from its entry. Keep runtime implementation imports out of these modules so behavior edits do not rebundle shared libraries.

After dependency setup, build all three outputs with one command from `Sample.Hosted/`:

```bash
npm run build:behaviors
```

For a targeted build, use `npm run build:behaviors:layout`, `npm run build:behaviors:home`, or `npm run build:behaviors:test`. The full `npm run build` includes the aggregate command. Each target cleans its own output directory and writes `.vite/manifest.json` there. These are independent builds, so they do not share generated chunks. With unchanged inputs and tooling, unchanged outputs retain their cache identities.

Deploy each changed area's hidden `.vite/manifest.json` with its emitted assets. For a `brotliOnly` entry, deploy `<entry>.[hash].js.br`. Otherwise, deploy `<entry>.[hash].js` and any emitted `.br` sidecar. Restart the application because it caches manifest paths and integrity hashes for the process lifetime. Preserve previously deployed hashed files until in-flight pages no longer reference them. Fresh pages use the new behavior URL. Unchanged bundles retain their URLs and browser caches. Already-open pages need a full reload to adopt new behavior code.

The initial migration needs the updated Razor views, rebuilt `appPostload`, and all three behavior outputs. Exclude the retired `wwwroot/appBehaviors/.vite/manifest.json` from the new deployment; the new targets do not clean that old directory. Retain old hashed assets separately while in-flight pages may still reference them. Later changes confined to one area's modules need only that area's output and manifest. Changes to runtime code, shared configuration, dependencies, or markup can require a broader deployment. The HTMX page retains inline scripts specifically for its nonce acceptance/rejection demonstration; early application configuration remains in the layout.

## Frontend tests

Unit tests are grouped by module under `buildwww/tests/unit/<module>/`, starting with `drn/drnCookieManager.test.ts`. `npm test` discovers `.test.js` and `.test.ts` files recursively under `buildwww/tests/`, including top-level suites. `npm run type-check` checks application code and the root Vite and Playwright configurations through `tsconfig.json`, then checks the test project through `tsconfig.tests.json`. Oxlint discovers the test settings through `buildwww/tests/tsconfig.json`. Playwright fixture specs live in `buildwww/tests/browser/`. Hosted application specs live in `buildwww/tests/hosted/`.

After installing and auditing dependencies as above, run frontend checks from `Sample.Hosted/`:

```bash
npm test
npm run test:browser
```

Run `npm run test:all` to start the unit and Playwright fixture suites in parallel. The command waits for both suites and fails if either fails. Playwright can also distribute individual fixture tests across workers. Use `npm run build:test:all` to build the assets first.

Add `--coverage` to print Node's coverage summary and write LCOV during the same unit run:

```bash
npm run test:all -- --coverage
npm run test:all -- --coverage --include 'buildwww/lib/**/*.ts' --exclude '**/*.d.ts' --report lib-coverage.info
```

`npm run test:coverage` collects unit coverage alone and accepts the same overrides. Each supplied filter replaces the corresponding default. Repeat `--include` or `--exclude` for multiple globs. The defaults include `buildwww/{app,lib}/**/*.{ts,tsx,js}`, exclude `**/*.d.ts`, and write `coverage-frontend.info` in the package directory. Node counts unloaded JS and TS files. The React unit tests execute TSX with source maps. Default coverage fails if any production TSX file lacks line data. Playwright runs separately from Node coverage.

CI builds the Sample web root once and uploads it with its Vite manifests. A separate Playwright job tests that artifact. Pull request and develop workflows collect unit coverage independently of the browser job. On master and release tags, Sonar and Playwright can start as soon as the assets are ready. The branch gatekeepers require Playwright to pass. Stable and preview releases verify the tag before building and publish the tested assets only after both Sonar and Playwright pass.

Node and Playwright print one terminal line with the suite, number passed out of the total, and duration. Failed, skipped, flaky, and unrun counts appear when nonzero. GitHub Actions shows the full counts in a table with expandable failure details. Successful unit coverage runs also show covered lines, total lines, and coverage percentage for all frontend sources and each file extension, including `.tsx`. The `frontend-validation` action publishes one coverage table per package from its LCOV report. Master and release workflows submit frontend LCOV and backend coverage in one Sonar scan. A missing report, a report without line data, or a failed quality gate blocks those workflows. Sonar configuration owns the thresholds.

Both `--report` and `PLAYWRIGHT_LCOV_REPORT` accept an `.info` or `.lcov` file inside the package working directory. Use a regular file with an existing parent directory. To include an existing report in a Playwright summary, set `PLAYWRIGHT_LCOV_REPORT` to its path. A missing or empty report fails the run. To exercise both reporter adapters locally, run `node buildwww/tests/run-tests.ts buildwww/tests/coverageSummary.test.ts buildwww/tests/reporters/summary.test.ts` from `Sample.Hosted`.

For multiple frontend packages, run the `frontend-validation` action once per package in the same job before the Sonar scan. Set each step's `working-directory` to the package directory and `coverage-report` to `coverage-frontend.info`. Each package writes its own file. Pass the report paths relative to the analysis directory in one `frontend-lcov` input, for example `Sample.Hosted/coverage-frontend.info,Admin.Hosted/coverage-frontend.info`. If the workflow sets `sonar-typescript-tsconfig-paths`, include each package's TSConfig there too. Sonar imports all the reports in the same scan, as it does for the .NET unit, analyzer, and integration reports.

The browser checks cover notification and form-error behavior without a running .NET app or database. `npm run test:browser` installs Playwright’s matching Chromium headless shell if needed. To install the shell ahead of time, run `npm run test:browser:install`. On Linux, run `npm exec --no --ignore-scripts -- playwright install-deps chromium` if system libraries are missing. CI installs those libraries before calling `test:browser`.

The fixture suite scans the expanded notification dialog and the profile form after a validation error with axe. It also checks keyboard navigation and focus.

For hosted browser tests, [start Sample.Hosted](#run-locally), then run `npm run test:browser:hosted` from `Sample.Hosted/`. The suite opens the anonymous Razor home page at `http://localhost:5998/`. It checks that rejecting optional cookies survives a reload and scans the full page for accessibility violations after the React island renders. Set `SAMPLE_HOSTED_URL` to use another running instance. Include the trailing slash when the application is hosted under a path prefix.
