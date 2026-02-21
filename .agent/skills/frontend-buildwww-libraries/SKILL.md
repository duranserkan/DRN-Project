---
name: frontend-buildwww-libraries
description: Frontend JavaScript architecture - DRN utilities (drnUtils, drnApp, drnCookieManager, drnOnmount), RSJS pattern for component mounting, htmx integration with CSP nonce security, and Bootstrap customization. Essential for client-side interactivity and component lifecycle. Keywords: javascript, rsjs, onmount, htmx, csp, nonce, bootstrap, cookie-management, component-mounting, client-side
last-updated: 2026-02-15
difficulty: intermediate
---

# buildwww Libraries

> JavaScript utilities, RSJS architecture, htmx integration, and Bootstrap customization in buildwww.

## When to Apply
- Using DRN JavaScript utilities
- Implementing RSJS for htmx (Reasonable System for JavaScript Structure)
- Working with htmx and CSP nonces
- Customizing Bootstrap styles
- Understanding client-side cookie management
- Setting up component mounting with `drnOnmount`

---

## RSJS Architecture & DrnOnmount

DRN follows [RSJS](https://github.com/rstacruz/rsjs): register behaviors that auto-attach to DOM elements via `data-` attributes or classes, instead of imperative initialization. Required for **htmx** — content swaps without full page reload.

### drnOnmount.js

`drnOnmount.js` — RSJS implementation. Wraps `onmount` with **idempotency** — prevents duplicate registration across htmx swaps.

#### API

```javascript
// Register a behavior for a selector
DRN.Onmount.register(selector, callback, idempotencyKey?);
```

- **selector**: CSS selector (e.g., `[data-bs-toggle="tooltip"]`, `#myComponent`).
- **callback**: Executed per matching element; `this` = element.
- **idempotencyKey** (optional): Prevents re-registration when scripts reload in partials.



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

**Example: `Sample.Hosted/Pages/Test/Htmx.cshtml`**

```razor
@section Scripts {
    <script>
        // Use a unique ID or class for the specific element on this page
        DRN.Onmount.register('#btnJqueryTest', function (options) {
            console.log("Button registered");

            // Attach event listener
            $(this).on('click', function () {
                $("#output").text("Clicked!");
            });

            // Cleanup is handled automatically on swap if jQuery is used, 
            // or use options.disposable for manual cleanup
        }, "uniqueHandlerId"); // <--- Idempotency Key
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

## DRN JavaScript Utilities

Global utilities are exposed under `window.DRN`.

### Directory Structure

```
buildwww/app/js/drn/
├── drnApp.js           # Application state and globals
├── drnUtils.js         # General utility functions
├── drnCookieManager.js # Cookie consent management
└── drnOnmount.js       # Component mounting system
```

### drnCookieManager.js

Handles cookie consent logic (Analytics/Marketing) and provides a wrapper for cookie operations.

```javascript
// Check specific consent
if (DRN.Cookie.hasConsent('Analytics')) {
    // load analytics scripts
}
```

---

## htmx Integration

### Security with CSP Nonces

DRN integrates htmx with a strict CSP using nonces.

**`htmxSafeNonce.js`**: Intercepts htmx requests to sync `htmx.config.inlineScriptNonce` with the current page nonce — allows inline scripts in partials, blocks unauthorized scripts.

```javascript
// buildwww/lib/htmx/htmxSafeNonce.js
htmx.config.inlineScriptNonce = document.currentScript?.nonce;
```

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
└── bootstrap.js        # JavaScript component imports
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

**`bootstrap.js`**:
Imports only necessary plugins to keep bundle size low.

```javascript
import 'bootstrap/js/dist/tooltip';
import 'bootstrap/js/dist/modal';
```

---

## Related Skills

- [frontend-buildwww-vite.md](../frontend-buildwww-vite/SKILL.md) - Vite configuration
- [drn-hosting.md](../drn-hosting/SKILL.md) - Security and TagHelpers
