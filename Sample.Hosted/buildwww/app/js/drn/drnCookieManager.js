import drnUtils from './drnUtils';

/**
 * DrnCookieManager
 * A robust, clean-code solution for client-side cookie management.
 * * Features:
 * - Automatic JSON serialization/deserialization
 * - Secure defaults (SameSite=Lax, Secure)
 * - URI Encoding/Decoding to prevent format errors
 * - Fluent options interface
 */
class DrnCookieManager {
    constructor() {
        // Default configuration
        this.defaults = {
            path: '/',
            sameSite: 'Strict',
            secure: window.location.protocol === 'https:',
            encoding: 'uri' // 'uri' | 'base64' | 'none'
        };
    }

    /**
     * Lists all cookies as { name, value, raw } objects.
     * @returns {Array<{name: string, value: string, raw: string}>}
     */
    list() {
        return document.cookie
            .split(';')
            .map(c => c.trimStart())
            .filter(c => c)
            .map(raw => {
                const eqIndex = raw.indexOf('=');
                return eqIndex > 0
                    ? { name: raw.slice(0, eqIndex), value: raw.slice(eqIndex + 1), raw }
                    : { name: raw, value: '', raw }; // malformed
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

        const config = {...this.defaults, ...options};
        let stringValue = value;

        if (typeof value === 'object' && value !== null) {
            try {
                stringValue = JSON.stringify(value);
            } catch (e) {
                console.error(`DrnCookieManager: Serialization failed for '${name}'`, e);
                return;
            }
        }
        // Encoding (String -> Safe String)
        let encodedValue = stringValue;
        if (config.encoding === 'uri') {
            encodedValue = encodeURIComponent(stringValue);
        } else if (config.encoding === 'base64') {
            encodedValue = drnUtils.urlSafeBase64Encode(stringValue);
        }

        // 3. Construct Cookie String
        // Note: We encode the name to prevent injection, though usually names are safe.
        let cookieString = `${encodeURIComponent(name)}=${encodedValue}`;

        // Expiration Logic: Max-Age takes precedence as it is more precise
        if (config.maxAge !== undefined) {
            cookieString += `; Max-Age=${config.maxAge}`;
        } else if (config.days) {
            const date = new Date();
            date.setTime(date.getTime() + (config.days * 24 * 60 * 60 * 1000));
            cookieString += `; expires=${date.toUTCString()}`;
        }

        if (config.domain) cookieString += `; domain=${config.domain}`;
        if (config.path) cookieString += `; path=${config.path}`;
        if (config.secure) cookieString += `; Secure`;
        if (config.sameSite) cookieString += `; SameSite=${config.sameSite}`

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
        // Use default encoding if not specified, or explicit override
        const encoding = options.encoding || this.defaults.encoding;
        const deserialize = options.deserialize || true;

        const encodedName = encodeURIComponent(name);
        const nameEQ = encodedName + '=';

        // Note: If document.cookie is empty, split returns [""]
        const ca = document.cookie.split(';');
        for (let i = 0; i < ca.length; i++) {
            let c = ca[i].trimStart();

            if (c.indexOf(nameEQ) === 0) {
                let value = c.substring(nameEQ.length, c.length);

                // 3. Decoding Strategy
                try {
                    if (encoding === 'uri')
                        value = decodeURIComponent(value);
                    else if (encoding === 'base64')
                        value = drnUtils.urlSafeBase64Decode?.(value) ?? value;
                } catch (e) {
                    console.warn(`Cookie read error [${name}]:`, e);
                    return null;
                }

                // 4. Deserialization
                if (deserialize) {
                    try {
                        return JSON.parse(value);
                    } catch (e) {
                        // Return raw value if JSON parse fails (graceful degradation)
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
        // Proceed anyway (idempotent)
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
        if (!name || typeof name !== 'string') {
            console.error("DrnCookieManager: Cookie name must be a non-empty string.");
            return false;
        }
        return true;
    }
}

export default new DrnCookieManager();