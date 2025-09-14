/**
 * Provides helper functions for registering components with the onmount library.
 * Assumes `onmount` global is available when register functions are *called*.
 */
const drnOnmount = {
    _registry: new Set(),
    /**
     * Default unregister function for onmount.
     * Cleans up event listeners and calls dispose/destroy if present.
     * @this {Element} - The DOM element being unmounted.
     * @param {Object} [options={}] - Options object.
     * @param {Object} [options.disposable] - An object with dispose/destroy methods.
     */
    unregister: function (options = {}) {
        if (!this) return;

        if (options.disposable && typeof options.disposable.dispose === 'function') {
            options.disposable.dispose();
        }
        if (options.disposable && typeof options.disposable.destroy === 'function') {
            options.disposable.destroy();
        }

        // Assumes jQuery ($) is available when onmount runs
        if (typeof $ !== 'undefined') {
            $(this).off(); // Remove all event handlers attached via jQuery on this element
        } else {
            console.warn("jQuery ($) is not available in unregister function.");
        }
    },

    /**
     * Registers a selector with onmount using default unregister logic.
     * Ensures registration happens after DOM is ready or onmount is loaded.
     * @param {string} selector - CSS selector for elements.
     * @param {Function} registerCallback - Callback to run on mount.
     * @param {string} [idempotencyKey=''] - Key to prevent duplicate registrations for the same selector.
     */
    register: function (selector, registerCallback, idempotencyKey = '') {
        // Delegate to registerFull with default unregister
        this.registerFull(selector, registerCallback, this.unregister, idempotencyKey); // Use drnOnmount reference
    },

    /**
     * Registers a selector with onmount using a custom unregister function.
     * Ensures registration happens after DOM is ready or onmount is loaded.
     * @param {string} selector - CSS selector for elements.
     * @param {Function} registerCallback - Callback to run on mount.
     * @param {Function} [unregisterCallback=drnOnmount.unregister] - Callback to run on unmount.
     * @param {string} [idempotencyKey=''] - Key to prevent duplicate registrations for the same selector.
     */
    registerFull: (selector, registerCallback, unregisterCallback = undefined, idempotencyKey = '') => {
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
        if (drnOnmount._registry.has(selectorKey)) {
            window.onmount();
            return;
        }

        // Add to registry
        drnOnmount._registry.add(selectorKey); // Use drnOnmount reference

        const executeRegistration = () => onmount(selector, registerCallback, unregisterCallback);


        // Handle execution timing
        let readyState = document.readyState;

        if (readyState === 'interactive' || readyState === 'complete')
            executeRegistration();
        else
            document.addEventListener('DOMContentLoaded', executeRegistration, {once: true});

        onmount();
    }
};

// Export the onmount object if needed by other modules
export default drnOnmount;