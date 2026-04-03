// htmxValidation.js
// Replaces jQuery + jQuery Validate + jQuery Validate Unobtrusive
// with aspnet-client-validation (~4 KB gzipped, zero dependencies).
//
// Custom providers must be registered BEFORE bootstrap() is called.
// See: https://andrewlock.net/adding-client-side-validation-to-aspnet-core-without-jquery-or-unobtrusive-validation/
//
// Usage — add a custom provider:
//   DRN.Validation.addProvider('endswith', (value, element, params) => {
//       if (!value) return true; // Let [Required] handle empty
//       return value.endsWith(params.value);
//   });

import { ValidationService } from 'aspnet-client-validation';

const validationService = new ValidationService();

// Expose hook for custom validation providers
// Register providers BEFORE bootstrap() — call from appPostload.js or page scripts
window.DRN = window.DRN || {};
window.DRN.Validation = validationService;

// Activate validation — scans the DOM for data-val-* attributes.
// ValidationService constructor captures document.body as root.
// When loaded in <head> (via htmx bundle), document.body is null.
// Defer bootstrap and pass root explicitly so it resolves correctly.
// watch: true enables MutationObserver to auto-scan new DOM nodes (htmx swaps).
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        validationService.bootstrap({ root: document.body, watch: true });
    });
} else {
    validationService.bootstrap({ watch: true });
}
