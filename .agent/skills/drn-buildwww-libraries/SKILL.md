---
name: drn-buildwww-libraries
description: "DRN buildwww JavaScript architecture - DRN browser utilities, onmount lifecycle, RSJS mounting, htmx CSP nonce security, and Bootstrap customization. Keywords: drn, buildwww, javascript, rsjs, onmount, htmx, csp, nonce, bootstrap, cookie-management, component-mounting"
last-updated: 2026-10-31
difficulty: intermediate
tokens: ~1.8K
---

# DRN buildwww Libraries

> JavaScript utilities, RSJS architecture, htmx integration, and Bootstrap customization in buildwww.
> DRN-scoped: use only when the repository profile or filesystem declares DRN `buildwww` and the DRN browser API surface.

## When to Apply
- Using repository JavaScript utilities
- Implementing RSJS for htmx (Reasonable System for JavaScript Structure)
- Working with htmx and CSP nonces
- Customizing Bootstrap styles
- Understanding client-side cookie management
- Setting up component mounting with the repository's onmount wrapper

---

## RSJS Architecture & DrnOnmount

This convention follows [RSJS](https://github.com/rstacruz/rsjs): register behaviors that auto-attach to DOM elements via `data-` attributes or classes, instead of imperative initialization. Required for **htmx** — content swaps without full page reload.

DRN buildwww exposes common shared browser utilities under `window.DRN`. Treat `DRN`, `Drn*`, and `DRN.Framework.*` names as shared framework API, not placeholders for app-specific namespaces.

### Onmount Wrapper

The onmount wrapper is an RSJS implementation. It wraps `onmount` with **idempotency** to prevent duplicate registration across htmx swaps.

#### API

```javascript
// Simple registration — uses default unregister (calls options.disposable.dispose())
DRN.Onmount.register(selector, callback, idempotencyKey?);

// Full registration — custom mount and unmount callbacks
DRN.Onmount.registerFull(selector, registerCallback, unregisterCallback, idempotencyKey?);
```

- **selector**: CSS selector (e.g., `[data-bs-toggle="tooltip"]`, `[data-js-my-island]`).
- **callback / registerCallback**: Executed per matching element; `this` = the DOM element.
- **unregisterCallback** (registerFull only): Custom cleanup on unmount; `this` = the DOM element. Call `DRN.Onmount.unregister.call(this, options)` at the end to invoke default disposal.
- **idempotencyKey** (optional): Prevents re-registration when scripts reload in partials.
- **options.disposable**: Set in register callback; default `unregister` calls `.dispose()` and `.destroy()` on it automatically.

> `register()` delegates to `registerFull()` with the default `unregister`. Use `registerFull()` when you need custom cleanup (e.g., clearing intervals, removing DOM listeners) — see [drn-buildwww-react](../drn-buildwww-react/SKILL.md) for the canonical example.

---

## Usage Patterns

### 1. Global Components (Standard RSJS)

Register global behaviors (e.g., Tooltips) in `appPostload.js` or a dedicated module.

```javascript
// buildwww/app/js/appPostload.js
DRN.Onmount.register('[data-bs-toggle="tooltip"]', function (options) {
    // 'this' is the element
    options.disposable = new bootstrap.Tooltip(this, {animation: false}); 
});
```

### 2. Page-Specific Logic (Augmented usage)

Define behaviors directly in Razor Pages `<script>` tags. Use `idempotencyKey` to safely re-execute on htmx swap without duplicate registration.

**Example: page-specific Razor logic**

```razor
@section Scripts {
    <script>
        DRN.Onmount.register('#refreshStatus', function (options) {
            const button = this;
            const onClick = () => button.dataset.clicked = "true";

            button.addEventListener('click', onClick);

            options.disposable = {
                dispose: () => button.removeEventListener('click', onClick)
            };
        }, "refreshStatusHandler");
    </script>
}
```

**Example: Localized Logic in Shared Components (`_Footer.cshtml`)**

```razor
<footer data-js-drn-footer>
    <!-- content -->
</footer>

<script>
    DRN.Onmount.register("[data-js-drn-footer]", function () {
        // Encapsulate logic for the footer here
        const footer = this;
        // ...
    });
</script>
```

---

## Application JavaScript Utilities

Global utilities are exposed under `window.DRN`.

Pass the owning application's route to `DRN.App.url(applicationRoute)` for browser requests. Resolve `applicationRoute` from that application's endpoints or configuration. Only `~/` paths receive the deployment prefix. External, fragment, relative, and already-resolved URLs stay unchanged. The layout must render `<meta name="drn-app-base" content="@Url.Content("~/")"/>` before the preload bundle so early error reporting can resolve URLs. Preserve controller route segments. Rebuild the owning frontend bundle after changing browser sources when execution is authorized.

### Directory Structure

```
buildwww/app/js/drn/
├── drnApp.js           # Application state and globals
├── drnUtils.js         # General utility functions
├── drnCookieManager.js # Cookie list/set/get/remove/exists wrapper
├── drnErrorHandler.js  # Early global error handling
├── drnOnmount.js       # Component mounting system
└── drnToast.js         # Bootstrap toast helper
```

### Problem Details Consumer

Use `DRN.Problem.fromResponse(response)` and `show(problem, context)` for Fetch errors. Context accepts `element`, `target`, `operation`, `persist`, and `focus`. HTMX validation errors appear in the originating form, a `data-problem-region`, or the page summary. Failures without validation errors use a brief toast and one history entry, with friendly text for generic HTTP titles. Preserve input and clear only the matching operation's errors. Resolve the complete browser API, parsing limits, and examples from the profile-declared application guide and declarations.

Use `DRN.ErrorHandler.reportActionError(error, { operation, message, persist })` only for caught browser failures affecting a user action. Keep public messages safe. Its clientErrorId is shared by the notification and backend report; backend Problem Details retain requestId. Global errors remain telemetry-only. HTMX failure feedback requires a trusted triggering event or explicit `data-notification-user-action="true"`; false suppresses feedback. Preserve copy/search support for both reference types and never reinterpret them as trace IDs.

Only `application/problem+json` is parsed, with the HTTP status taking precedence. Display public messages as text and retain a generic fallback for unusable responses. Do not retry mutations automatically or swap JSON as HTML. An HTML validation swap requires status 422, `text/html`, and `X-DRN-Validation: true`. Hosting's `HandleHtmxRequests` option controls error formatting without changing authentication behavior.

Validation keys match complete posted names, including casing, prefixes, and collection indices. An empty key is form-level feedback. Keep Razor response selection explicit and preserve existing API validation keys. See [Hosting error guidance](../drn-hosting/SKILL.md#error-responses) for server-side conventions.

### Notification History

Use `DRN.Notifications.publish` to retain a message without a toast. Use `DRN.Toast.show` for a toast and history entry, `record: false` for toast-only feedback, or `persist: false` for memory-only history. Forms and components can set `data-notification-operation` and `data-notification-persist`. Server handlers use `SendToast`. Direct client feedback uses `DRN.Toast.show`.

Choose immediate presentation and retention independently:

- Routine acknowledgments, such as copying text: toast only with `record: false`.
- Results worth revisiting, such as export completion or background failure: toast and history.
- Updates that need no immediate attention: history only with `DRN.Notifications.publish`.
- Server-reported validation: contextual feedback through `DRN.Problem` and one history entry, with a toast fallback when no inline region exists.
- Request failures without validation errors: a brief toast and one history entry through `DRN.Problem`, without a page banner.

Do not repeat inline feedback with an additional toast or record routine client-side field validation in history. Omit feedback when neither immediate attention nor later review is useful. `persist: false` controls storage only, not toast visibility or whether history is recorded. Keep the decision guide in the profile-declared application documentation aligned with these rules.

Keep history bounded and isolated by application and account. Publish only safe public text. Account changes clear history and contextual errors. Stale pages pause until reloaded. Closing feedback and dismissing history are separate actions. Resolve retention limits, panel behavior, and API options from the profile-declared application guide and browser declarations.

Keep the notification bell visible. Its badge counts unseen entries and hides at zero. Opening the panel marks existing history seen; later arrivals remain unseen until it is opened again. Persist seen state, preserve it through Undo, and keep deduplicated source identities seen. New publications always start unseen.

Use Bootstrap `text-bg-warning` on action buttons needing attention and `text-bg-danger` for count badges. Render the normal button color initially and apply warning only after reading saved attendance, so acknowledged items do not flash yellow on navigation. On `shown.bs.offcanvas`, remove only the opened item's warning class, preserving its other classes and counts. Notifications additionally mark existing history seen and hide the zero unseen badge. Keep severity colors inside history. When displaying a request reference, briefly explain that users can share it with support to locate the request.

Persist notification history and other actionbar attendance in local storage so new and existing tabs share the latest state. Scope attendance by application/account and validate the notification history owner. Refresh on storage events without replaying toasts. Serialize notification read/modify/write operations under an application-scoped Web Lock. Merge operation deltas against the latest saved history inside the lock, recheck ownership there, and never let a suspended account overwrite another account's history. Keep the public notification API synchronous while persistence queues asynchronously. Protect pending local changes from storage-event refreshes. Without Web Locks, use tab-local session storage. Storage or lock failures retain current-page feedback in memory. Keep `persist: false` notifications local to the page. Compare the acknowledged activity version, falling back to `data-actionbar-count` text. Capped or unchanged counts need an explicit `data-actionbar-attention-version` on the button to distinguish new activity. Blocked storage falls back to current-page memory.

Persist actionbar attendance under independent panel keys within the application/account scope. Read legacy map entries as fallback, but never replace that shared map when acknowledging a panel. Preserve failed writes in current-page memory. Notification removal deltas must match nonempty source identities as well as IDs, because concurrent source deduplication can retain another tab's ID.

Authenticated notification owners require a stable account ID. Never substitute a request trace ID or the anonymous owner. An empty owner clears history and suspends notifications and HTMX requests until a fresh account context is supplied. Preserve anonymous history for unauthenticated users.

Anchor toasts in the positioned `data-toast-host` content wrapper, outside its scrolling content, to avoid covering the header or actionbar. Use opaque Bootstrap `bg-body` surfaces and constrain the stack to the host's width and height. Pages without a host use bottom-center viewport placement.

Keep toast containers below Bootstrap offcanvas and modal backdrops so feedback cannot obscure panel controls. Apply the same stacking order to content-host and viewport placement.

Toast `duration` controls presentation only. Timeout, manual close, visible-toast eviction, and HTMX cleanup must preserve retained history. Explicit history dismissal must not be undone by subsequent toast cleanup. Cover these boundaries with the browser fixture and real Bootstrap lifecycle.

### Cookie Manager

Provides low-level cookie operations. Consent preferences are stored by Razor/footer code using `DRN.Cookie.get()` and `DRN.Cookie.set()` with the server-provided consent cookie name.

```javascript
const preferences = DRN.Cookie.get(preferencesCookieName, {encoding: 'base64'}) || {};
if (preferences.AnalyticsConsent) {
    // load analytics scripts
}
```

---

## Script Loading Order

All scripts load in a specific order defined in `_LayoutBase.cshtml`. This order is critical — each layer depends on the previous:

```
appPreload.js          → DRN.App, DRN.Utils, DRN.Onmount, DRN.React = {} namespace
  ↓
<inline script>        → DRN.App config (Environment, CsrfToken, Cultures)
  ↓
htmxBundle.js          → htmx + safe-nonce extension + client validation
  ↓
bootstrapBundle.js     → Bootstrap JS plugins
  ↓
appPostload.js         → Global DRN.Onmount registrations (tooltips, etc.)
  ↓
reactBundle.tsx         → DRN.React.mount() + component registry
  ↓
@section Scripts { }   → Page-specific registrations (DRN.Onmount.register/registerFull)
```

> **Dependency rule**: `appPreload.js` must execute first — it initializes `window.DRN` so all subsequent scripts can attach to the shared framework browser API. If a bundle loads before `window.DRN` is ready, it logs a critical error.

---

## htmx Integration

### Security with CSP Nonces

This convention integrates htmx with a strict CSP using nonces.

`_LayoutBase.cshtml` sets `htmx.config.inlineScriptNonce` through the `htmx-config` meta tag. `htmxSafeNonce.js` defines the `safe-nonce` extension used by `hx-ext="safe-nonce"`: it reads `HX-Nonce` or CSP nonce headers from htmx responses and removes swapped `<script>` tags whose nonce does not match.

```javascript
// buildwww/lib/htmx/htmxSafeNonce.js
htmx.defineExtension('safe-nonce', {
    transformResponse: function (text, xhr, elt) {
        // parse response and remove scripts without the response nonce
    }
});
```

### Client Validation

`htmxBundle.js` imports `htmxValidation.js`, which uses `aspnet-client-validation` with `watch: true` so htmx swaps are scanned automatically. Keep validation on this direct `data-val-*` path.

### Razor Usage

```razor
<!-- Auto-includes request verification token (CSRF) via headers if configured -->
<button hx-post="~@Get.Page.Test.Htmx?handler=Auto"
        hx-target="#result">
    Submit
</button>
```

---

## Bootstrap Customization

Bootstrap styles are customized via SCSS and compiled by Vite.

### Structure

```
buildwww/lib/bootstrap/
├── bootstrap.scss      # Variable overrides and imports
└── bootstrapBundle.js  # JavaScript component imports
```

### Configuration

**`bootstrap.scss`: existing stylesheet pattern**:

The relative paths below reflect existing Sass imports. For new imports, follow [Package Imports](../drn-buildwww-packages/SKILL.md#package-imports). Migrate these paths only after verifying package resolution and the frontend build.

```scss
// 1. Configure default variables
$primary: #0750bc;

// 2. Existing relative Sass imports, retained pending migration verification
@import "../../../node_modules/bootstrap/scss/functions";
@import "../../../node_modules/bootstrap/scss/variables";
@import "../../../node_modules/bootstrap/scss/maps";
@import "../../../node_modules/bootstrap/scss/mixins";
@import "../../../node_modules/bootstrap/scss/utilities";

// 3. Merge Bootstrap Icons into the same CSS bundle
@import "../../../node_modules/bootstrap-icons/font/bootstrap-icons";
```

**`bootstrapBundle.js`**:
Imports the selected Bootstrap plugins and exposes them under `window.bootstrap`.

```javascript
import Alert from 'bootstrap/js/dist/alert'
import Button from 'bootstrap/js/dist/button'
import Modal from 'bootstrap/js/dist/modal'
import Tooltip from 'bootstrap/js/dist/tooltip'
import Toast from 'bootstrap/js/dist/toast'
import Collapse from 'bootstrap/js/dist/collapse'
import Dropdown from 'bootstrap/js/dist/dropdown'
import Tab from 'bootstrap/js/dist/tab'
import Popover from 'bootstrap/js/dist/popover'
import Offcanvas from 'bootstrap/js/dist/offcanvas'
```

---

## Related Skills

- [drn-buildwww-vite.md](../drn-buildwww-vite/SKILL.md) - Vite configuration
- [drn-buildwww-react.md](../drn-buildwww-react/SKILL.md) - React mounted islands (primary consumer of `registerFull`)
- Framework-scoped hosting/security skill declared by `.agent/repository-profile.md`, when present.
