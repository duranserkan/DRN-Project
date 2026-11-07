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

export interface DrnCookieReadOptions {
    /** Attempt JSON deserialization. Defaults to true. */
    deserialize?: boolean;
    /** Must match the encoding used when setting the cookie. Defaults to 'uri'. */
    encoding?: NonNullable<DrnCookieOptions['encoding']>;
}

export interface IDrnCookieManager {
    list(): Array<{ name: string; value: string; raw: string }>;

    /** Serializes object values as JSON before encoding. */
    set(name: string, value: string | object, options?: DrnCookieOptions): void;

    /** Returns null if the cookie is missing or decoding fails. */
    get<T = string>(name: string, options?: DrnCookieReadOptions): T | null;

    /** Use the same path and domain that were used to set the cookie. */
    remove(name: string, options?: DrnCookieOptions): void;

    /** Checks presence without decoding or deserializing the value. */
    exists(name: string): boolean;
}
