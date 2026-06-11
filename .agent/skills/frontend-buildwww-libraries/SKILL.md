---
name: frontend-buildwww-libraries
description: "Frontend JavaScript architecture - application utilities, onmount lifecycle, RSJS pattern for component mounting, htmx integration with CSP nonce security, and Bootstrap customization. Essential for client-side interactivity and component lifecycle. Keywords: javascript, rsjs, onmount, htmx, csp, nonce, bootstrap, cookie-management, component-mounting, client-side"
last-updated: 2026-06-12
difficulty: intermediate
tokens: ~1.8K
---

# buildwww Libraries

> JavaScript utilities, RSJS architecture, htmx integration, and Bootstrap customization in buildwww.
> Convention-scoped: use only when the repository profile or filesystem declares `buildwww` and the DRN browser API surface.

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

> `register()` delegates to `registerFull()` with the default `unregister`. Use `registerFull()` when you need custom cleanup (e.g., clearing intervals, removing DOM listeners) — see [frontend-buildwww-react](../frontend-buildwww-react/SKILL.md) for the canonical example.

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

### Directory Structure

```
buildwww/app/js/drn/
├── drnApp.js           # Application state and globals
├── drnUtils.js         # General utility functions
├── drnCookieManager.js # Cookie consent management
└── drnOnmount.js       # Component mounting system
```

### Cookie Manager

Handles cookie consent logic (Analytics/Marketing) and provides a wrapper for cookie operations.

```javascript
// Check specific consent
if (DRN.Cookie.hasConsent('Analytics')) {
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
htmxBundle.js          → htmx + CSP nonce sync
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

**`htmxSafeNonce.js`**: Intercepts htmx requests to sync `htmx.config.inlineScriptNonce` with the current page nonce — allows inline scripts in partials, blocks unauthorized scripts.

```javascript
// buildwww/lib/htmx/htmxSafeNonce.js
htmx.config.inlineScriptNonce = document.currentScript?.nonce;
```

### Client Validation

`htmxBundle.js` imports `htmxValidation.js`, which uses `aspnet-client-validation` with `watch: true` so htmx swaps are scanned automatically. Keep validation on this direct `data-val-*` path.

### Razor Usage

```razor
<!-- Auto-includes request verification token (CSRF) via headers if configured -->
<button hx-post="@Get.Page.Test.Htmx?handler=Auto"
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

**`bootstrap.scss`**:
```scss
// 1. Configure default variables
$primary: #0d6efd;

// 2. Import Bootstrap
@import "bootstrap/scss/bootstrap";

// 3. Add custom overrides
.btn-primary { ... }
```

**`bootstrapBundle.js`**:
Imports only necessary plugins to keep bundle size low.

```javascript
import 'bootstrap/js/dist/tooltip';
import 'bootstrap/js/dist/modal';
```

---

## Related Skills

- [frontend-buildwww-vite.md](../frontend-buildwww-vite/SKILL.md) - Vite configuration
- [frontend-buildwww-react.md](../frontend-buildwww-react/SKILL.md) - React mounted islands (primary consumer of `registerFull`)
- Framework-scoped hosting/security skill declared by `.agent/repository-profile.md`, when present.
