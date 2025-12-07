/**
 * Options for setting a cookie.
 */
export interface DrnCookieOptions {

    /**
     * Expiration in seconds (takes precedence over days)
     * If omitted, creates a session cookie.
     */
    maxAge?: number;

    /**
     * Number of days until the cookie expires.
     * If omitted, creates a session cookie.
     */
    days?: number;

    /**
     * The path where the cookie is available.
     * Defaults to '/' to ensure visibility across the app.
     */
    path?: string;

    /**
     * The domain where the cookie is available.
     */
    domain?: string;

    /**
     * If true, the cookie is only sent over HTTPS.
     * Defaults to true if the current page is served over HTTPS.
     */
    secure?: boolean;

    /**
     * Controls whether the cookie is sent with cross-origin requests.
     * Defaults to 'Strict'.
     * 'None' requires Secure: true.
     */
    sameSite?: 'Strict' | 'Lax' | 'None';

    /**
     * The encoding strategy.
     * Defaults to 'uri'.
     */
    encoding?: 'uri' | 'base64' | 'none';
}

export interface IDrnCookieManager {
    list(): Array<{ name: string, value: string, raw: string }>;

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
    set(name: string, value: string | object, options?: DrnCookieOptions): void;

    /**
     * Retrieves a cookie value by name.
     * @param {string} name - The name of the cookie.
     * @param {object} [options] - Retrieval options.
     * @param {boolean} [options.deserialize=false] - If true, attempts to JSON.parse.
     * @param {'uri'|'base64'|'none'} [options.encoding='uri'] - Decoding strategy matching the set strategy.
     * @returns {string|object|null} The value or null if not found.
     */
    get<T = string>(name: string, options?: object): T | null;

    /**
     * Deletes a cookie by setting its expiration to the past.
     * IMPORTANT: You must provide the same Path and Domain used when setting it.
     * @param name - The key of the cookie.
     * @param options - Path and Domain configuration.
     */
    remove(name: string, options?: CookieOptions): void;

    /**
     * Boolean check for cookie existence.
     * @param name - The key of the cookie.
     */
    exists(name: string): boolean;
}

declare const DrnCookieManager: IDrnCookieManager;