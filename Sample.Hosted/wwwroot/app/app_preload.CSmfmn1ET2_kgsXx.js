const drnApp = {
  Environment: "Neitherland",
  IsDev: false,
  ShowCookieBanner: false,
  CsrfToken: "",
  DefaultCulture: "tr",
  SupportedCultures: ["en", "tr"],
  // Placeholder for application state management
  State: {}
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
    try {
      const base64 = btoa(str);
      return base64.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
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
    if (typeof str !== "string") {
      console.warn("urlSafeBase64Decode: Input must be a string.");
      return "";
    }
    const base64 = str.replace(/-/g, "+").replace(/_/g, "/").padEnd(str.length + (4 - str.length % 4) % 4, "=");
    return atob(base64);
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
class DrnCookieManager {
  constructor() {
    this.defaults = {
      path: "/",
      sameSite: "Strict",
      secure: window.location.protocol === "https:",
      encoding: "uri"
      // 'uri' | 'base64' | 'none'
    };
  }
  /**
   * Lists all cookies as { name, value, raw } objects.
   * @returns {Array<{name: string, value: string, raw: string}>}
   */
  list() {
    return document.cookie.split(";").map((c) => c.trimStart()).filter((c) => c).map((raw) => {
      const eqIndex = raw.indexOf("=");
      return eqIndex > 0 ? { name: raw.slice(0, eqIndex), value: raw.slice(eqIndex + 1), raw } : { name: raw, value: "", raw };
    });
  }
  /**
   * Sets a cookie with the specified name, value, and options.
   * @param {string} name - The name of the cookie.
   * @param {string|object} value - The value to store. Objects are automatically stringified.
   * @param {DrnCookieOptions} [options] - Configuration options.
   * @param {number} [options.days] - Expiration in days.
   * @param {number} [options.maxAge] - Expiration in seconds (takes precedence over days).
   * @param {string} [options.path='/'] - The path scope (defaults to root).
   * @param {string} [options.domain] - The domain scope.
   * @param {boolean} [options.secure] - If true, cookie requires HTTPS. Defaults to location.protocol === 'https:'.
   * @param {'Strict'|'Lax'|'None'} [options.sameSite='Strict'] - CSRF protection level
   * @param {'uri'|'base64'|'none'} [options.encoding='uri'] - Encoding strategy.
   */
  set(name, value, options = {}) {
    if (!this._validateName(name)) return;
    const config = { ...this.defaults, ...options };
    let stringValue = value;
    if (typeof value === "object" && value !== null) {
      try {
        stringValue = JSON.stringify(value);
      } catch (e) {
        console.error(`DrnCookieManager: Serialization failed for '${name}'`, e);
        return;
      }
    }
    let encodedValue = stringValue;
    if (config.encoding === "uri") {
      encodedValue = encodeURIComponent(stringValue);
    } else if (config.encoding === "base64") {
      encodedValue = drnUtils.urlSafeBase64Encode(stringValue);
    }
    let cookieString = `${encodeURIComponent(name)}=${encodedValue}`;
    if (config.maxAge !== void 0) {
      cookieString += `; Max-Age=${config.maxAge}`;
    } else if (config.days) {
      const date = /* @__PURE__ */ new Date();
      date.setTime(date.getTime() + config.days * 24 * 60 * 60 * 1e3);
      cookieString += `; expires=${date.toUTCString()}`;
    }
    if (config.domain) cookieString += `; domain=${config.domain}`;
    if (config.path) cookieString += `; path=${config.path}`;
    if (config.secure) cookieString += `; Secure`;
    if (config.sameSite) cookieString += `; SameSite=${config.sameSite}`;
    document.cookie = cookieString;
  }
  /**
   * Retrieves a cookie value by name.
   * @param {string} name - The name of the cookie.
   * @param {object} [options] - Retrieval options.
   * @param {boolean} [options.deserialize=true] - If true, attempts to JSON.parse.
   * @param {'uri'|'base64'|'none'} [options.encoding='uri'] - Decoding strategy matching the set strategy.
   * @returns {string|object|null} The value or null if not found.
   */
  get(name, options = {}) {
    if (!this._validateName(name)) return null;
    const encoding = options.encoding || this.defaults.encoding;
    options.deserialize || true;
    const encodedName = encodeURIComponent(name);
    const nameEQ = encodedName + "=";
    const ca = document.cookie.split(";");
    for (let i = 0; i < ca.length; i++) {
      let c = ca[i].trimStart();
      if (c.indexOf(nameEQ) === 0) {
        let value = c.substring(nameEQ.length, c.length);
        try {
          if (encoding === "uri")
            value = decodeURIComponent(value);
          else if (encoding === "base64")
            value = drnUtils.urlSafeBase64Decode?.(value) ?? value;
        } catch (e) {
          console.warn(`Cookie read error [${name}]:`, e);
          return null;
        }
        {
          try {
            return JSON.parse(value);
          } catch (e) {
            return value;
          }
        }
        return value;
      }
    }
    return null;
  }
  /**
   * Deletes a cookie.
   * IMPORTANT: Path and Domain must match how the cookie was set.
   * @param {string} name - The name of the cookie.
   * @param {object} [options] - Must match path/domain of original cookie.
   */
  remove(name, options = {}) {
    if (!this.exists(name))
      console.warn(`DrnCookieManager: Attempted to remove non-existent cookie '${name}'`);
    this.set(name, "", { ...options, maxAge: 0 });
  }
  /**
   * Checks if a specific cookie exists.
   * @param {string} name
   * @returns {boolean}
   */
  exists(name) {
    return this.get(name) !== null;
  }
  // --- Internal Helpers ---
  _validateName(name) {
    if (!name || typeof name !== "string") {
      console.error("DrnCookieManager: Cookie name must be a non-empty string.");
      return false;
    }
    return true;
  }
}
const drnCookieManager = new DrnCookieManager();
if (typeof window !== "undefined") {
  window.DRN = window.DRN || {};
  window.DRN.App = drnApp;
  window.DRN.Onmount = drnOnmount;
  window.DRN.Utils = drnUtils;
  window.DRN.Cookie = drnCookieManager;
  document.addEventListener("popstate", () => {
    window.location.href = window.location.href;
  });
}
