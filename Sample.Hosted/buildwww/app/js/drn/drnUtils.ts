import type { IDrnUtils } from '../../../types/drnUtils.types.ts';

/**
 * Provides common helper functions
 */
const drnUtils: IDrnUtils = {
    urlSafeBase64Encode: (str: string) => {
        if (typeof str !== 'string') {
            console.warn('urlSafeBase64Encode: Input must be a string.');
            return '';
        }
        try {
            const base64 = btoa(str);
            return base64
                .replaceAll('+', '-')  // Replace '+' with '-'
                .replaceAll('/', '_')  // Replace '/' with '_'
                .replace(/={1,2}$/, '');
        } catch (e) {
            console.error("urlSafeBase64Encode: Base64 encoding failed", e);
            return str;
        }
    },

    urlSafeBase64Decode: (str: string) => {
        if (typeof str !== 'string') {
            console.warn('urlSafeBase64Decode: Input must be a string.');
            return '';
        }

        const base64 = str
            .replaceAll('-', '+')  // Replace '-' with '+'
            .replaceAll('_', '/')  // Replace '_' with '/'
            .padEnd(str.length + (4 - str.length % 4) % 4, '=');  // Add padding if necessary

        return atob(base64);
    },

    /** Includes the tag, ID, classes, and data attributes. Returns 'Unknown Element' for a missing element. */
    getRequestElementSelector: (requestElement: Element | null) => {
        if (!(requestElement instanceof Element)) return 'Unknown Element';

        let selectorSuffix = '';
        if (requestElement.id)
            selectorSuffix += `#${requestElement.id}`;
        if (requestElement.classList.length > 0)
            selectorSuffix += `.${[...requestElement.classList].join('.')}`;

        Array.from(requestElement.attributes)
            .filter(attr => attr.name.startsWith('data-'))
            .forEach(dataAttr => {
                selectorSuffix += `[${dataAttr.name}="${dataAttr.value}"]`;
            });

        return requestElement.tagName + selectorSuffix;
    },

    isPlainObject: (value: unknown): value is Record<string, unknown> => Object.prototype.toString.call(value) === '[object Object]',

    /**
     * Deeply merges two objects, with override properties taking precedence over base.
     *
     * - Base serves as the foundation object
     * - Overrides provide new values and take precedence
     * - When both have the same property:
     *   - For plain objects: recursively merge them
     *   - Otherwise: override value wins
     * - Returns a new object; does not mutate inputs
     *
     * @example
     * const base = { name: 'Duran', config: { theme: 'dark', size: 'large' } };
     * const updates = { age: 25, config: { theme: 'light' } };
     * const result = deepMerge(base, updates);
     * // Returns: { name: 'Duran', age: 25, config: { theme: 'light', size: 'large' } }
     */
    deepMerge: <T extends object, U extends object>(base: T, overrides: U): T & U => {
        if (!overrides) return base as T & U;
        if (!base) return overrides as T & U;

        const output: Record<string, unknown> = { ...base } as Record<string, unknown>;

        for (const key in overrides) {
            if (key === '__proto__' || key === 'constructor' || key === 'prototype')
                continue;
            if (Object.hasOwn(overrides, key)) {
                const overrideValue = overrides[key];
                const baseValue = output[key];

                if (drnUtils.isPlainObject(overrideValue) && drnUtils.isPlainObject(baseValue))
                    output[key] = drnUtils.deepMerge(baseValue, overrideValue);
                else
                    output[key] = overrideValue;
            }
        }

        return output as T & U;
    },

    /** Escapes text for insertion into HTML element content. */
    escapeHtml: (str: string) => {
        const div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }
};

// Export the utils object if needed by other modules
export default drnUtils;
