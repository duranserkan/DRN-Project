// htmxValidation.ts
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

/**
 * aspnet-client-validation 0.11.1 removes listeners for detached inputs but
 * retains the corresponding DOM nodes in its UID indexes. The service is
 * page-scoped, so those strong references would otherwise survive every htmx
 * swap. Keep the package behavior while releasing all indexes for the removed
 * subtree after its normal cleanup completes.
 */
class DrnValidationService extends ValidationService {
    override remove(root?: ParentNode) {
        // aspnet-client-validation keeps these runtime indexes private in its declarations.
        const indexes = this as unknown as {
            options?: { root?: ParentNode };
            elementUIDs: { node: Node; uid: string }[];
            formInputs: Record<string, string[]>;
            formEvents: Record<string, { remove(): void }>;
            inputEvents: Record<string, { remove(): void }>;
            messageFor: Record<string, unknown>;
            validators: Record<string, unknown>;
            summary: Record<string, unknown>;
            elementByUID: Record<string, unknown>;
        };
        const removalRoot = root ?? indexes.options?.root ?? document.body;
        const elementUIDs = Array.isArray(indexes.elementUIDs) ? indexes.elementUIDs : [];
        const removedUIDs = new Set(
            elementUIDs
                .filter((entry) => entry?.node === removalRoot || removalRoot?.contains?.(entry?.node))
                .map((entry) => entry.uid),
        );

        super.remove(removalRoot);

        if (removedUIDs.size === 0) return;

        // Remove detached input UIDs from forms that remain outside the swapped subtree.
        for (const [formUID, inputUIDs] of Object.entries(indexes.formInputs || {})) {
            const remainingInputUIDs = inputUIDs.filter((uid) => !removedUIDs.has(uid));
            if (remainingInputUIDs.length > 0) {
                indexes.formInputs[formUID] = remainingInputUIDs;
                continue;
            }

            indexes.formEvents?.[formUID]?.remove?.();
            delete indexes.formEvents?.[formUID];
            delete indexes.formInputs[formUID];
            delete indexes.messageFor?.[formUID];
            removedUIDs.add(formUID);
        }

        for (const uid of removedUIDs) {
            indexes.formEvents?.[uid]?.remove?.();
            indexes.inputEvents?.[uid]?.remove?.();
            delete indexes.formEvents?.[uid];
            delete indexes.formInputs?.[uid];
            delete indexes.messageFor?.[uid];
            delete indexes.inputEvents?.[uid];
            delete indexes.validators?.[uid];
            delete indexes.summary?.[uid];
            delete indexes.elementByUID?.[uid];
        }

        indexes.elementUIDs = elementUIDs.filter((entry) => !removedUIDs.has(entry.uid));
    }
}

const validationService = new DrnValidationService();

// Expose hook for custom validation providers
// Register providers BEFORE bootstrap() — call from appPostload.ts or page scripts
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
