import drnUtils from './drnUtils.ts';

import type {DrnCookieOptions, DrnCookieReadOptions, IDrnCookieManager} from '@/types/drnCookieManager.types';

/**
 * DrnCookieManager
 * A robust, clean-code solution for client-side cookie management.
 * * Features:
 * - Automatic JSON serialization/deserialization
 * - SameSite=Strict by default, Secure on HTTPS
 * - URI Encoding/Decoding to prevent format errors
 * - Fluent options interface
 */
class DrnCookieManager implements IDrnCookieManager {
    defaults: Required<Pick<DrnCookieOptions, 'path' | 'sameSite' | 'secure' | 'encoding'>>;

    constructor() {
        // Default configuration
        this.defaults = {
            path: '/',
            sameSite: 'Strict',
            secure: window.location.protocol === 'https:',
            encoding: 'uri' // 'uri' | 'base64' | 'none'
        };
    }

    list(): Array<{name: string, value: string, raw: string}> {
        return document.cookie
            .split(';')
            .map(c => c.trimStart())
            .filter(Boolean)
            .map(raw => {
                const eqIndex = raw.indexOf('=');
                return eqIndex > 0
                    ? {name: raw.slice(0, eqIndex), value: raw.slice(eqIndex + 1), raw}
                    : {name: raw, value: '', raw}; // malformed
            });
    }

    set(name: string, value: string | object, options: DrnCookieOptions = {}): void {
        if (!this._validateName(name)) return;

        const config = {...this.defaults, ...options};
        let stringValue: string;

        if (typeof value === 'object' && value != null) {
            try {
                stringValue = JSON.stringify(value);
            } catch (e) {
                console.error(`DrnCookieManager: Serialization failed for '${name}'`, e);
                return;
            }
        } else {
            stringValue = String(value);
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

    _findRawCookieValue(name: string): string | null {
        const nameEQ = encodeURIComponent(name) + '=';
        const ca = document.cookie.split(';');
        for (const item of ca) {
            const c = item.trimStart();
            if (c.startsWith(nameEQ)) {
                return c.substring(nameEQ.length);
            }
        }
        return null;
    }

    _decodeCookieValue(value: string, encoding: DrnCookieOptions['encoding'], name: string): string | null {
        try {
            if (encoding === 'uri')
                return decodeURIComponent(value);
            if (encoding === 'base64')
                return drnUtils.urlSafeBase64Decode?.(value) ?? value;
            return value;
        } catch (e) {
            console.warn(`Cookie read error [${name}]:`, e);
            return null;
        }
    }

    _parseCookieValue(value: string, deserialize: boolean): unknown {
        if (!deserialize) return value;
        try {
            return JSON.parse(value);
        } catch {
            return value;
        }
    }

    get<T = string>(name: string, options: DrnCookieReadOptions = {}): T | null {
        if (!this._validateName(name)) return null;
        const encoding = options.encoding || this.defaults.encoding;
        const deserialize = options.deserialize ?? true;

        const rawValue = this._findRawCookieValue(name);
        if (rawValue === null) return null;

        const decodedValue = this._decodeCookieValue(rawValue, encoding, name);
        if (decodedValue === null) return null;

        // Preserve the caller-selected type contract; cookie contents are not schema-validated.
        return this._parseCookieValue(decodedValue, deserialize) as T | null;
    }

    remove(name: string, options: DrnCookieOptions = {}): void {
        if (!this.exists(name))
            console.warn(`DrnCookieManager: Attempted to remove non-existent cookie '${name}'`);
        // Proceed anyway (idempotent)
        this.set(name, "", {...options, maxAge: 0});
    }
    
    exists(name: string): boolean {
        if (!this._validateName(name)) return false;
        return this._findRawCookieValue(name) !== null;
    }

    // --- Internal Helpers ---
    _validateName(name: unknown): name is string {
        if (!name || typeof name !== 'string') {
            console.error("DrnCookieManager: Cookie name must be a non-empty string.");
            return false;
        }
        return true;
    }
}

export default new DrnCookieManager();
