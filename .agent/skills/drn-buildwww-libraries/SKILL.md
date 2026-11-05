---
name: drn-buildwww-libraries
description: "DRN browser behaviors. Keywords: drn, buildwww, rsjs, onmount, htmx, csp"
last-updated: 2026-11-05
difficulty: intermediate
---

# DRN buildwww Libraries

Keep AGENTS.md, DiSCOS, and the profile active. The application guide and browser contracts own public APIs and limits. Follow [type ownership](../drn-buildwww-vite/references/type-ownership.md) and [test conventions](../drn-buildwww-vite/references/frontend-tests.md).

## Contents

- [RSJS Architecture & DrnOnmount](#rsjs-architecture--drnonmount)
- [Usage Patterns](#usage-patterns)
- [Application JavaScript Utilities](#application-javascript-utilities)
- [Script Loading Order](#script-loading-order)
- [htmx Integration](#htmx-integration)
- [Bootstrap Customization](#bootstrap-customization)

## RSJS Architecture & DrnOnmount

Follow [RSJS](https://github.com/rstacruz/rsjs): declarative HTML hooks and separately bundled JavaScript behaviors. Prefer `data-js-*` selectors and queries scoped to the mounted element. Keep one behavior per module. The shared framework API is `window.DRN`; `DRN`, `Drn*`, and `DRN.Framework.*` are real names, not placeholders.

### Onmount Wrapper

`DRN.Onmount.register(selector, callback, idempotencyKey)` uses default cleanup. `registerFull(selector, callback, unregisterCallback, idempotencyKey)` accepts custom cleanup. The key is optional. Registration is deduplicated by selector and key, so page bundles can execute again on htmx navigation.

- Mount callbacks receive `options`; `this` is the matching DOM element.
- Set `options.disposable` to the owned resource. Default unregister calls its `dispose()` and `destroy()` methods when present.
- For custom unregister, clear timers/listeners first, then call `DRN.Onmount.unregister.call(this, options)`.
- Keep mutable state per mounted element. Use a composite disposable when a behavior owns several resources.
- Initial loading must poll after deferred registrations. Existing htmx polling activates new elements and cleans removed ones. Preserve the body-swap guard so callbacks do not create portals while old body children are being removed.
- Idempotency prevents duplicate registration, not downloads or execution. A previously loaded behavior remains registered until full reload. Changed code needs a full reload to replace it.

## Usage Patterns

### Global and Page-Specific Components

Group modules under `buildwww/app/js/behaviors/{layout,home,test}/`. Domain entry files import modules without importing runtime implementations. The [Vite skill](../drn-buildwww-vite/SKILL.md#entry-points) owns builds and outputs.

| Entry | Components | Loading |
|---|---|---|
| `layoutBehaviors.js` | Footer, tooltips | Global shell after runtime bundles |
| `homeBehaviors.js` | HelloReact island | HomeAnonymous page `Scripts` section |
| `testBehaviors.js` | Browser exception demo | HTMX test page `Scripts` section |

Put component logic in modules and encoded data in Razor. Keep inline nonce-test fixtures as explicit test exceptions and early configuration before its consumers.

```javascript
// behaviors/layout/tooltip.js
DRN.Onmount.register('[data-bs-toggle="tooltip"]', function (options) {
    options.disposable = new bootstrap.Tooltip(this, {animation: false});
});
```

See [React integration](../drn-buildwww-react/SKILL.md#razor-page-integration) for encoded JSON attributes. Read the mounted element's dataset. Never bypass encoding with raw HTML. Layouts forward/render `Scripts` for full and htmx responses, preserving nonce, SRI, and PathBase.

## Application JavaScript Utilities

Shared runtime modules live under `buildwww/app/js/drn/`. Use `DRN.App.url(applicationRoute)` for requests. Only `~/` receives the deployment prefix; external, fragment, relative, and resolved URLs stay unchanged. Render `<meta name="drn-app-base" content="@Url.Content("~/")"/>` before preload and preserve controller segments. Rebuild affected bundles only with execution permission.

### Problem Details Consumer

Use `DRN.Problem.fromResponse(response)` and `show(problem, context)` for Fetch failures. Context accepts `element`, `target`, `operation`, `persist`, and `focus`. Match validation keys to full posted names, including case, prefixes, and collection indexes; empty keys are form-level. Preserve input and clear only matching operation errors.

- Parse only `application/problem+json`; HTTP status takes precedence. Render public text safely with generic fallback. Never retry mutations automatically or swap JSON as HTML.
- HTML validation swaps require status 422, `text/html`, and `X-DRN-Validation: true`. Hosting's `HandleHtmxRequests` controls formatting, not authentication.
- Bound incremental Fetch clone reads before buffering. Preserve streaming UTF-8 decoding and the original response. Do not await clone cancellation while the original can remain unread.
- HTMX validation appears in the originating form, a `data-problem-region`, or summary, with one history entry and toast fallback only when no inline region exists. Other failures use one brief toast and history entry, without a banner.
- Require a trusted triggering event or `data-notification-user-action="true"` for HTMX failure feedback; `false` suppresses it. HTTP errors/server toast headers do not generate client-error reports. Transport failures remain client errors.

Use `DRN.ErrorHandler.reportActionError(error, {operation, message, persist})` only for caught failures affecting user actions. Its `clientErrorId` is shared by notification/backend report. Backend Problem Details use `requestId`. Preserve copy/search support and never reinterpret either as a trace ID. See [hosting errors](../drn-hosting/SKILL.md#error-responses).

### Diagnostics and Privacy

Global failures create one generic history entry and a toast with `record: false` when notifications are active. Apply shared rate limits/deduplication before publication. Bound visible toasts without queues/history deletion. Send diagnostics only for a rendered authenticated owner with an active store. Enforce endpoint authentication/per-user quotas, bound UTF-8 reports, and contain reporting failures.

Browser capture imports the shared classification catalog at build time. Keep browser/analyzer baselines aligned. Server `DiagnosticLogging:RuleFile` does not configure browser rules; applications use the client configuration API. Browser name/URL matching precedes clipping. Backend runtime uses explicit classifications without name/path matching. Preserve free text and capture budgets without content scanning. Keep browser/backend golden cases separate.

Browser protected URL values use `redacted`; bare fragments do not set the flag. Backend explicit URL capture validates encoding and removes credentials without name matching; explicit value classifications retain `[redacted]`. Bound browser arrays by position to 256 entries plus a truncation marker, sparse holes as null, inspecting own descriptors without getters/inherited properties. Preserve dense/sparse regression coverage.

Keep backend diagnostics out of notifications. Browser `clientErrorDetails` stay bounded and associated with `clientErrorId`. Preserve original values except page URLs, which require redaction before display/storage. Render details as text only after Show details. Capture the original stack once; never synthesize a handler stack. The profile-declared browser guide owns display ordering, stack formatting/fallback, and saved-detail compatibility.

### Notification History

Use `DRN.Toast.show` for toast/history, `record: false` for routine acknowledgments, `DRN.Notifications.publish` for history alone, and `persist: false` for page-local history. Server feedback uses `SendToast`. Forms may set `data-notification-operation` and `data-notification-persist`. Avoid duplicate inline/toast feedback and routine validation history. The application guide owns limits, panel behavior, and formatting.

- Bound history and isolate by application/account. Empty authenticated owners clear history and suspend notifications/HTMX until fresh context; require stable account IDs, never trace IDs or anonymous substitutes. Preserve anonymous history for unauthenticated users.
- Account changes clear history, errors, prior-account DOM, and Undo. Stale pages pause until reload. Retain bounded startup feedback only through first valid configuration, preserving timestamps/references and merging persistence deltas; discard on invalid owner/prior suspension and never carry across accounts.
- Keep the bell visible, count unseen entries, hide zero badge. Opening marks existing entries seen; later arrivals remain unseen. Persist seen state through Undo/deduplication; new publications start unseen. Preserve immediate badges, deferred closed-panel rendering, accessible filters, and colors defined by the application guide.
- Persist under an application-scoped Web Lock. Merge against latest storage and recheck owner inside the lock. Suspended pages cannot overwrite another owner. Keep public APIs synchronous, persistence asynchronous, pending changes protected, and synchronous deltas ordered without merging browser tasks. Follow source-owned expiry/sizing rules.
- Without Web Locks use tab-local session storage. Storage/lock failures retain feedback in memory. Keep `persist: false` page-local. Storage refresh must not replay toasts. Removal deltas match nonempty source identities as well as IDs.
- Store actionbar attendance under independent app/account/panel keys. Read legacy values without replacing the map. Preserve failed writes in memory. Compare `data-actionbar-attention-version`, falling back to count text; capped/unchanged counts need explicit versions to signal new activity.
- Preserve source-owned actionbar colors/attendance behavior and support-reference explanations. Toasts stay opaque, bounded to `data-toast-host` outside scrolling content or bottom-center viewport fallback, and below modal/offcanvas backdrops. Duration, close, eviction, and HTMX cleanup affect presentation only, never history or explicit dismissals. Preserve browser lifecycle coverage.

### Cookie Manager

The footer uses `DRN.Cookie.get`/`set` with base64 encoding and its element's server-provided cookie name. Keep instance configuration local; resolve the cookie API from `types/drnCookieManager.types.ts`.

## Script Loading Order

The full shell loads preload, early `DRN.App` configuration, htmx, Bootstrap, postload, React, then layout behaviors. Preload initializes `window.DRN` and `DRN.React` before dependents. Postload installs application-wide listeners and deferred DOM work. Page `Scripts` sections load home/test behaviors after content. React stays a global runtime; splitting behavior bundles does not lazy-load React itself.

## htmx Integration

### Security with CSP Nonces

The shell's `htmx-config` configures script/style nonces. The `safe-nonce` extension reads `HX-Nonce` or CSP headers and removes swapped scripts whose nonce does not match. Use normal Vite-backed script references. Preserve request verification tokens and never weaken CSP/CSRF to load a bundle.

### Client Validation

`htmxValidation.js` uses `aspnet-client-validation` with `watch: true` to scan swapped `data-val-*` content. Register custom providers before bootstrap. Keep this direct path and release detached input/form indexes with normal cleanup.

## Bootstrap Customization

`buildwww/lib/bootstrap/bootstrap.scss` owns variable overrides, imports, and Bootstrap Icons. `bootstrapBundle.js` exposes selected plugins under `window.bootstrap`. Follow [package imports](../drn-buildwww-packages/SKILL.md#package-imports) for new imports. Do not migrate relative Sass imports without verifying resolution and an authorized build.

## Related Skills

- [Vite builds](../drn-buildwww-vite/SKILL.md)
- [React islands](../drn-buildwww-react/SKILL.md)
- [Razor layouts](../frontend-razor-pages-shared/SKILL.md)
