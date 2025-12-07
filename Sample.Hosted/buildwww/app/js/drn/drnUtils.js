/**
 * Provides common helper functions
 */
const drnUtils = {
    /**
     * Encodes a string to URL-safe Base64.
     * @param {string} str - The string to encode.
     * @returns {string} The URL-safe Base64 encoded string.
     */
    urlSafeBase64Encode: (str) => {
        if (typeof str !== 'string') {
            console.warn('urlSafeBase64Encode: Input must be a string.');
            return '';
        }
        try {
            const base64 = btoa(str);
            return base64
                .replace(/\+/g, '-')  // Replace '+' with '-'
                .replace(/\//g, '_')  // Replace '/' with '_'
                .replace(/=+$/, '');
        } catch (e) {
            console.error("urlSafeBase64Encode: Base64 encoding failed", e);
            return str;
        }
    },

    /**
     * Decodes a URL-safe Base64 string.
     * @param {string} str - The URL-safe Base64 string to decode.
     * @returns {string} The decoded string.
     */
    urlSafeBase64Decode: (str) => {
        if (typeof str !== 'string') {
            console.warn('urlSafeBase64Decode: Input must be a string.');
            return '';
        }

        const base64 = str
            .replace(/-/g, '+')  // Replace '-' with '+'
            .replace(/_/g, '/')  // Replace '_' with '/'
            .padEnd(str.length + (4 - str.length % 4) % 4, '=');  // Add padding if necessary

        return atob(base64);
    },

    /**
     * Generates a CSS selector string representing the given element.
     * Includes tag name, ID, classes, and data attributes.
     * @param {Element} requestElement - The DOM element.
     * @returns {string} A selector string, or 'Unknown Element'.
     */
    getRequestElementSelector: (requestElement) => {
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

    /**
     * Checks if a value is a plain object (not an array, function, Date, etc.)
     * Only objects created by the Object constructor are considered plain objects.
     *
     * @param {any} value - The value to check
     * @returns {boolean} True if the value is a plain object, false otherwise
     *
     * @example
     * isPlainObject({}) // true
     * isPlainObject([]) // false
     * isPlainObject(new Date()) // false
     */
    isPlainObject: (value) => Object.prototype.toString.call(value) === '[object Object]',

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
     * @param {Object} base - Base object that provides default structure/values
     * @param {Object} overrides - Object containing values that should take precedence
     * @returns {Object} New merged object combining both inputs
     *
     * @example
     * const base = { name: 'Duran', config: { theme: 'dark', size: 'large' } };
     * const updates = { age: 25, config: { theme: 'light' } };
     * const result = deepMerge(base, updates);
     * // Returns: { name: 'Duran', age: 25, config: { theme: 'light', size: 'large' } }
     */
    deepMerge: (base, overrides) => {
        if (!overrides) return base;
        if (!base) return overrides;

        const output = {...base};

        for (const key in overrides) {
            if (Object.prototype.hasOwnProperty.call(overrides, key)) {
                const overrideValue = overrides[key];
                const baseValue = output[key];

                if (drnUtils.isPlainObject(overrideValue) && drnUtils.isPlainObject(baseValue))
                    output[key] = drnUtils.deepMerge(baseValue, overrideValue);
                else
                    output[key] = overrideValue;
            }
        }

        return output;
    }
};

// Export the utils object if needed by other modules
export default drnUtils;