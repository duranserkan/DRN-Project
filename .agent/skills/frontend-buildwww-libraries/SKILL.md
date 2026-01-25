---
name: frontend-buildwww-libraries
description: Frontend JavaScript architecture - DRN utilities (drnUtils, drnApp, drnCookieManager, drnOnmount), RSJS pattern for component mounting, htmx integration with CSP nonce security, and Bootstrap customization. Essential for client-side interactivity and component lifecycle. Keywords: javascript, frontend, rsjs, onmount, htmx, csp, nonce, security, bootstrap, cookie-management, component-mounting, client-side, skills, frontend buildwww vite, drn hosting
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

DRN follows the [RSJS (Reasonable System for JavaScript Structure)](https://github.com/rstacruz/rsjs) philosophy. The core idea is to treat standard HTML with `data-` attributes or specific classes as the source of truth for behavior. Instead of initializing components imperatively in a main script, we register "behaviors" that automatically attach to elements when they appear in the DOM.

This is critical for applications using **htmx**, where content is swapped dynamically without a full page reload.

### drnOnmount.js

`drnOnmount.js` is the implementation of the RSJS pattern in DRN. It wraps the `onmount` library to provide a robust registration system with **idempotency** support.

#### API

```javascript
// Register a behavior for a selector
DRN.Onmount.register(selector, callback, idempotencyKey?);
```

- **selector**: CSS selector to match elements (e.g., `[data-bs-toggle="tooltip"]` or `#myComponent`).
- **callback**: Function executed for each matching element. `this` refers to the element.
- **idempotencyKey** (Optional): A string key to ensure a specific behavior is registered only once for a given selector. This is useful when scripts are included in partial views or Razor pages that might be re-loaded.

#### Internal Structure

`drnOnmount` maintains a `_registry` of `selector + '_' + idempotencyKey` to prevent duplicate registration logic from running, which avoids memory leaks and double-binding events.

```javascript
// Sample.Hosted/buildwww/app/js/drn/drnOnmount.js
const drnOnmount = {
    _registry: new Set(),
    
    // ... unregister logic ...

    registerFull: (selector, registerCallback, unregisterCallback = undefined, idempotencyKey = '') => {
        const selectorKey = selector + '_' + idempotencyKey;

        // Check for idempotency to avoid re-registering existing behaviors
        if (drnOnmount._registry.has(selectorKey)) {
            window.onmount(); // Just re-run onmount to catch new elements
            return;
        }

        drnOnmount._registry.add(selectorKey);
        
        // Execute registration using the underlying onmount library
        // ...
    }
};
```

---

## Usage Patterns

### 1. Global Components (Standard RSJS)

For behaviors that apply globally (like Tooltips), register them in `appPostload.js` or a dedicated module.

```javascript
// buildwww/app/js/appPostload.js
DRN.Onmount.register('[data-bs-toggle="tooltip"]', function (options) {
    // 'this' is the element
    options.disposable = new bootstrap.Tooltip(this, {animation: false}); 
});
```

### 2. Page-Specific Logic (Augmented usage)

You can define behaviors directly inside Razor Pages using `<script>` tags. Use the `idempotencyKey` to safely re-execute the script when HTMX swaps the page, without duplicating the registration.

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

DRN integrates htmx with a strict Content Security Policy (CSP) using nonces.

**`htmxSafeNonce.js`**:
Intercepts htmx requests to ensure that `htmx.config.inlineScriptNonce` matches the current page's nonce. This allows htmx to safely execute inline scripts returned in partials (like the Razor examples above) while blocking unauthorized scripts.

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

Bootstraps styles are customized via SCSS and then compiled by Vite.

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
