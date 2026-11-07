import type { IDrnOnmount } from '@/types/drnOnmount.types';

const registrationKeys = new Set<string>();

/**
 * Provides helper functions for registering components with the onmount library.
 * Assumes `onmount` global is available when register functions are *called*.
 */
const drnOnmount: IDrnOnmount & { _isHtmxSwapping(): boolean } = {
    listRegistrationKeys() {
        return [...registrationKeys];
    },

    /**
     * Returns true when document.body is mid-htmx swap.
     *
     * htmx's swapInnerHTML inserts new content before old children, then removes
     * all old children — including any elements appended to document.body by
     * component initializers (e.g. Syncfusion popups). Detecting this state lets
     * us skip premature onmount() trigger calls; the existing htmx:load handler
     * in appPostload.ts will call onmount() after the swap completes.
     */
    _isHtmxSwapping() {
        return document.body?.classList.contains('htmx-swapping') ?? false;
    },
    /** Calls the disposable’s cleanup methods when the element is unmounted. */
    unregister: function (options = {}) {
        if (!this) return;

        if (options.disposable && typeof options.disposable.dispose === 'function') {
            options.disposable.dispose();
        }
        if (options.disposable && typeof options.disposable.destroy === 'function') {
            options.disposable.destroy();
        }

        // Event handler cleanup:
        // - Component-level cleanup is handled by dispose()/destroy() above
        // - For vanilla JS listeners, use AbortController or register cleanup in options.disposable
    },

    /** Uses the default disposable cleanup on unmount. */
    register: function (selector, registerCallback, idempotencyKey = '') {
        // Delegate to registerFull with default unregister
        this.registerFull(selector, registerCallback, this.unregister, idempotencyKey); // Use drnOnmount reference
    },

    /** Defers registration until the DOM is ready and registers each selector and idempotency key once. */
    registerFull: (selector, registerCallback, unregisterCallback, idempotencyKey = '') => {
        // Basic input validation
        if (typeof selector !== 'string' || selector.trim() === '') {
            console.error('drnOnmount.registerFull: Invalid selector provided:', selector);
            return;
        }

        if (typeof registerCallback !== 'function') {
            console.error('drnOnmount.registerFull: Registration callback must be a function.');
            return;
        }
        if (unregisterCallback !== undefined && typeof unregisterCallback !== 'function') {
            unregisterCallback = drnOnmount.unregister; // Use default if invalid
        }
        if (typeof idempotencyKey !== 'string') {
            console.error('drnOnmount.registerFull: Idempotency key must be a string.');
            return;
        }

        const selectorKey = selector + '_' + idempotencyKey;

        // Check for idempotency
        if (registrationKeys.has(selectorKey)) {
            if (!drnOnmount._isHtmxSwapping()) { window.onmount(); }
            return;
        }

        registrationKeys.add(selectorKey);

        const executeRegistration = () => onmount(selector, registerCallback, unregisterCallback);


        // Handle execution timing
        let readyState = document.readyState;

        if (readyState === 'interactive' || readyState === 'complete')
            executeRegistration();
        else
            document.addEventListener('DOMContentLoaded', executeRegistration, { once: true });

        if (!drnOnmount._isHtmxSwapping()) { onmount(); }
    }
};

// Export the onmount object if needed by other modules
export default drnOnmount;
