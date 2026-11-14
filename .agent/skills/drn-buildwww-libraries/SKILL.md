---
name: drn-buildwww-libraries
description: "DRN browser behaviors. Keywords: drn, buildwww, rsjs, onmount, htmx, csp"
last-updated: 2026-11-13
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

Use the [RSJS](https://github.com/rstacruz/rsjs) pattern: declarative HTML hooks and separately bundled behaviors. Prefer `data-js-*` selectors and queries scoped to the mounted element. Keep one behavior per module. The shared API is `window.DRN`. Preserve the real `DRN`, `Drn*`, and `DRN.Framework.*` names.

### Onmount Wrapper

`DRN.Onmount.register(selector, callback, idempotencyKey)` uses default cleanup. `registerFull(selector, callback, unregisterCallback, idempotencyKey)` accepts custom cleanup. Both deduplicate by selector and optional key. Inspect a snapshot with `DRN.Onmount.listRegistrationKeys()`.

- Mount callbacks receive `options`. `this` is the matching DOM element. Return `false` to retry on a later poll when a dependency is not ready.
- Set `options.disposable` to the owned resource. Default unregister calls its `dispose()` and `destroy()` methods when present.
- For custom unregister, clear timers/listeners first, then call `DRN.Onmount.unregister.call(this, options)`.
- Keep mutable state per mounted element. Use a composite disposable when a behavior owns several resources.
- Initial loading must poll after deferred registrations. Existing htmx polling activates new elements and cleans removed ones. Preserve the body-swap guard so callbacks do not create portals while old body children are being removed.
- Idempotency prevents duplicate registration, not downloads or execution. A previously loaded behavior remains registered until full reload. Changed code needs a full reload to replace it.

## Usage Patterns

### Global and Page-Specific Components

Group modules under `buildwww/app/ts/behaviors/{layout,home,test}/`. Entries import their modules without runtime implementations. The [Vite skill](../drn-buildwww-vite/SKILL.md#entry-points) owns entry names, loading, and outputs.

Put component logic in modules and encoded data in Razor. Keep inline nonce-test fixtures as explicit test exceptions and early configuration before its consumers.

```javascript
// behaviors/layout/tooltip.ts
DRN.Onmount.register('[data-bs-toggle="tooltip"]', function (options) {
    options.disposable = new bootstrap.Tooltip(this, {animation: false});
});
```

See [React integration](../drn-buildwww-react/SKILL.md#razor-page-integration) for encoded JSON attributes. Read the mounted element's dataset. Never bypass encoding with raw HTML. Layouts forward/render `Scripts` for full and htmx responses, preserving nonce, SRI, and PathBase.

## Application JavaScript Utilities

Use `DRN.App.url(applicationRoute)` for requests. Only `~/` receives the deployment prefix. External, fragment, relative, and resolved URLs stay unchanged. Render `<meta name="drn-app-base" content="@Url.Content("~/")"/>` before preload and preserve controller segments. Rebuild affected bundles only with execution permission.

### Problem Details Consumer

Use `DRN.Problem.fromResponse(response)` and `DRN.Problem.show(problem, context)` for Fetch failures. Context accepts `element`, `target`, `operation`, `persist`, and `focus`. Match validation keys to full posted names, including case, prefixes, and collection indexes. Empty keys are form-level. Preserve input and clear only matching operation errors.

- Parse only `application/problem+json`. HTTP status takes precedence. Render public text safely with generic fallback. Never retry mutations automatically or swap JSON as HTML.
- HTML validation swaps require status 422, `text/html`, and `X-DRN-Validation: true`. Hosting's `HandleHtmxRequests` controls formatting, not authentication.
- Preserve the caller's response body. Oversized, malformed, or non-problem responses use generic text. Reading a bounded clone must not wait for the caller to consume the original.
- HTMX validation appears in the originating form, a `data-problem-region`, or summary, with one history entry and toast fallback only when no inline region exists. Other failures use one brief toast and history entry, without a banner.
- Require a trusted triggering event or `data-notification-user-action="true"` for HTMX failure feedback. `false` suppresses it. HTTP errors/server toast headers do not generate client-error reports. Transport failures remain client errors.

Use `DRN.ErrorHandler.reportActionError(error, {operation, message, persist})` only for caught failures affecting user actions. Its `clientErrorId` is shared by notification/backend report. Backend Problem Details use `requestId`. Preserve copy/search support and never reinterpret either as a trace ID. See [hosting errors](../drn-hosting/SKILL.md#error-responses).

### Diagnostics and Privacy

The profile-declared browser guide owns capture limits, display formatting, and saved-detail compatibility. Check it when changing diagnostics.

- Global failures produce one generic history entry and a toast with `record: false`. Preserve shared rate limits and deduplication.
- Send bounded diagnostics only for a rendered authenticated owner with an active store. Preserve independent endpoint authentication and per-user quotas. Reporting failure must not replace the original error or suppress local feedback.
- Browser rules come from the shared catalog at build time or `DRN.Diagnostics.configure`. Server `DiagnosticLogging:RuleFile` does not configure them. Apply browser name/URL redaction before clipping. Backend capture uses explicit classifications. Free text is bounded without content scanning.
- Protected browser URL values use `redacted`. Bare fragments do not set the flag. Arrays retain at most 256 positions plus a truncation marker. Sparse holes become null. Do not invoke getters or inspect inherited properties.
- Keep backend diagnostics out of notifications. Associate bounded `clientErrorDetails` with `clientErrorId`. Preserve original values except page URLs, which require redaction before display/storage. Show details as text on demand. Preserve the original stack without synthesizing a handler stack.

### Notification History

Use `DRN.Toast.show` for toast/history, `record: false` for routine acknowledgments, `DRN.Notifications.publish` for history alone, and `persist: false` for page-local history. Server feedback uses `SendToast`. Forms may set `data-notification-operation` and `data-notification-persist`. Avoid duplicate inline/toast feedback and routine validation history. The application guide owns limits, panel behavior, and formatting.

- Bound and isolate history by application/account. Require stable authenticated account IDs, never trace IDs or anonymous substitutes. Empty authenticated owners clear history and suspend notifications/HTMX until fresh context. Preserve anonymous history for unauthenticated users.
- Account changes clear history, contextual errors, prior-account DOM, and Undo. Stale pages pause until reload. Keep startup feedback only through the first valid configuration, retaining timestamps/references. Discard it on an invalid owner or prior suspension. Never carry it across accounts.
- Show the bell only after MFA. Its unseen count hides at zero. Opening marks existing entries seen, while later arrivals remain unseen. Preserve seen state through Undo and deduplication. Update badges immediately and keep filters accessible.
- Public APIs update feedback synchronously. Persistence is asynchronous and preserves concurrent publications, seen changes, dismissals, and Undo across tabs. Dismissal covers duplicate source references. Without Web Locks, use tab-local session storage. Storage/lock failure keeps feedback in memory. Storage refresh never replays toasts.
- Actionbar attendance persists independently per application, account, and panel. Preserve existing attendance and memory fallback on failure. Set `data-actionbar-attention-version` when capped or unchanged counts must signal new activity.
- Toasts stay opaque, bounded to `data-toast-host` or the viewport fallback, and below modal/offcanvas backdrops. Excess presentation never queues or deletes history. Duration, closing, eviction, and HTMX cleanup do not dismiss history. Preserve the guide's retention limits, colors, and support-reference explanations.

### Cookie Manager

The footer uses `DRN.Cookie.get`/`set` with base64 encoding and its element's server-provided cookie name. Keep configuration local. Resolve the API from `buildwww/types/drnCookieManager.types.ts`.

## Script Loading Order

The full shell loads preload, early `DRN.App` configuration, htmx, Bootstrap, postload, React, then layout behaviors. Preload provides `DRN.React.mount`, which returns `null` until React loads. Postload installs application-wide listeners and deferred DOM work. Page `Scripts` sections load home/test behaviors after content. React stays global across pages.

## htmx Integration

### Security with CSP Nonces

Preserve `htmx-config` nonces, Vite asset references, and CSRF tokens. The profile’s Sample guide owns the HTMX navigation rules.

### Client Validation

The htmx bundle installs `DRN.Validation` (`ValidationService` from `aspnet-client-validation`). Register providers after installation and before bootstrap. Use `watch: true` for swapped `data-val-*` content and release detached input/form indexes during cleanup.

## Bootstrap Customization

`buildwww/lib/bootstrap/bootstrap.scss` owns variable overrides, imports, and Bootstrap Icons. `bootstrapBundle.ts` exposes selected plugins under `window.bootstrap`. Follow [package imports](../drn-buildwww-packages/SKILL.md#package-imports) for new imports. Do not migrate relative Sass imports without verifying resolution and an authorized build.

## Related Skills

- [Vite builds](../drn-buildwww-vite/SKILL.md)
- [React islands](../drn-buildwww-react/SKILL.md)
- [Razor layouts](../frontend-razor-pages-shared/SKILL.md)
