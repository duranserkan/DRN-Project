// nonceHelper.ts

let cachedNonce: string | null = null;

const getNonceFromHtmxContent = (content: string): string => {
    try {
        const parsed = JSON.parse(content);
        if (typeof parsed?.inlineStyleNonce === 'string' && parsed.inlineStyleNonce) {
            return parsed.inlineStyleNonce;
        }
        if (typeof parsed?.inlineScriptNonce === 'string') {
            return parsed.inlineScriptNonce;
        }
    } catch {
        // ignore JSON parse errors
    }
    return '';
};

const getNonceFromHtmxMeta = (): string => {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="htmx-config"]');
    if (!meta) return '';

    const attrNonce = meta.getAttribute('inlineStyleNonce') || meta.getAttribute('inlineScriptNonce');
    if (attrNonce) return attrNonce;

    return meta.content ? getNonceFromHtmxContent(meta.content) : '';
};

const getNonceFromDomElements = (): string => {
    const nonceElement = document.querySelector<HTMLElement>('script[nonce], style[nonce]');
    return nonceElement?.nonce || nonceElement?.getAttribute('nonce') || '';
};

/**
 * Discovers and memoizes the inline style/script CSP nonce from HTMX meta configuration or DOM elements.
 */
export const getInlineStyleNonce = (): string => {
    if (cachedNonce !== null) return cachedNonce;

    if (typeof document === 'undefined') return '';

    const nonce = getNonceFromHtmxMeta() || getNonceFromDomElements();
    if (nonce) {
        cachedNonce = nonce;
        if (typeof window !== 'undefined') {
            (window as any).__webpack_nonce__ = nonce;
        }
    }

    return nonce;
};
