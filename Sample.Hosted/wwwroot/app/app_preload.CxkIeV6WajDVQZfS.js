const drnApp = {
  environment: "Neitherland",
  isDev: false,
  showCookieBanner: false,
  csrfToken: "",
  defaultCulture: "tr",
  supportedCultures: ["en", "tr"],
  // Placeholder for application state management
  state: {}
};
const drnOnmount = {
  _registry: /* @__PURE__ */ new Set(),
  /**
   * Default unregister function for onmount.
   * Cleans up event listeners and calls dispose/destroy if present.
   * @this {Element} - The DOM element being unmounted.
   * @param {Object} [options={}] - Options object.
   * @param {Object} [options.disposable] - An object with dispose/destroy methods.
   */
  unregister: function(options = {}) {
    if (!this) return;
    if (options.disposable && typeof options.disposable.dispose === "function") {
      options.disposable.dispose();
    }
    if (options.disposable && typeof options.disposable.destroy === "function") {
      options.disposable.destroy();
    }
    if (typeof $ !== "undefined") {
      $(this).off();
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
  register: function(selector, registerCallback, idempotencyKey = "") {
    this.registerFull(selector, registerCallback, this.unregister, idempotencyKey);
  },
  /**
   * Registers a selector with onmount using a custom unregister function.
   * Ensures registration happens after DOM is ready or onmount is loaded.
   * @param {string} selector - CSS selector for elements.
   * @param {Function} registerCallback - Callback to run on mount.
   * @param {Function} [unregisterCallback=drnOnmount.unregister] - Callback to run on unmount.
   * @param {string} [idempotencyKey=''] - Key to prevent duplicate registrations for the same selector.
   */
  registerFull: (selector, registerCallback, unregisterCallback = void 0, idempotencyKey = "") => {
    if (typeof selector !== "string" || selector.trim() === "") {
      console.error("drnOnmount.registerFull: Invalid selector provided:", selector);
      return;
    }
    if (typeof registerCallback !== "function") {
      console.error("drnOnmount.registerFull: Registration callback must be a function.");
      return;
    }
    if (unregisterCallback !== void 0 && typeof unregisterCallback !== "function") {
      unregisterCallback = drnOnmount.unregister;
    }
    if (typeof idempotencyKey !== "string") {
      console.error("drnOnmount.registerFull: Idempotency key must be a string.");
      return;
    }
    const selectorKey = selector + "_" + idempotencyKey;
    if (drnOnmount._registry.has(selectorKey)) {
      window.onmount();
      return;
    }
    drnOnmount._registry.add(selectorKey);
    const executeRegistration = () => onmount(selector, registerCallback, unregisterCallback);
    let readyState = document.readyState;
    if (readyState === "interactive" || readyState === "complete")
      executeRegistration();
    else
      document.addEventListener("DOMContentLoaded", executeRegistration, { once: true });
    onmount();
  }
};
const drnUtils = {
  /**
   * Encodes a string to URL-safe Base64.
   * @param {string} str - The string to encode.
   * @returns {string} The URL-safe Base64 encoded string.
   */
  urlSafeBase64Encode: (str) => {
    if (typeof str !== "string") {
      console.warn("urlSafeBase64Encode: Input must be a string.");
      return "";
    }
    const base64 = btoa(str);
    return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  },
  /**
   * Decodes a URL-safe Base64 string.
   * @param {string} str - The URL-safe Base64 string to decode.
   * @returns {string} The decoded string.
   */
  urlSafeBase64Decode: (str) => {
    if (typeof str !== "string") {
      console.warn("urlSafeBase64Decode: Input must be a string.");
      return "";
    }
    const base64 = str.replace(/-/g, "+").replace(/_/g, "/").padEnd(str.length + (4 - str.length % 4) % 4, "=");
    return atob(base64);
  },
  /**
   * Checks if a cookie with the given name exists.
   * @param {string} cookieName - The name of the cookie to check.
   * @returns {boolean} True if the cookie exists, false otherwise.
   */
  checkCookieExists: (cookieName) => {
    if (typeof cookieName !== "string") return false;
    return document.cookie.split("; ").some((cookie) => cookie.startsWith(`${cookieName}=`));
  },
  /**
   * Generates a CSS selector string representing the given element.
   * Includes tag name, ID, classes, and data attributes.
   * @param {Element} requestElement - The DOM element.
   * @returns {string} A selector string, or 'Unknown Element'.
   */
  getRequestElementSelector: (requestElement) => {
    if (!(requestElement instanceof Element)) return "Unknown Element";
    let selectorSuffix = "";
    if (requestElement.id)
      selectorSuffix += `#${requestElement.id}`;
    if (requestElement.classList.length > 0)
      selectorSuffix += `.${[...requestElement.classList].join(".")}`;
    Array.from(requestElement.attributes).filter((attr) => attr.name.startsWith("data-")).forEach((dataAttr) => {
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
  isPlainObject: (value) => Object.prototype.toString.call(value) === "[object Object]",
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
    const output = { ...base };
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
if (typeof window !== "undefined") {
  window.DRN = window.DRN || {};
  window.DRN.App = drnApp;
  window.DRN.Onmount = drnOnmount;
  window.DRN.Utils = drnUtils;
  document.addEventListener("popstate", () => {
    window.location.href = window.location.href;
  });
}
