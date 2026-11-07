/*
 * This file is licensed to you under the BSD Zero Clause License.
 * Source codes under this file obtained and modified from https://github.com/MichaelWest22/htmx-extensions/blob/main/src/safe-nonce/safe-nonce.js
 */

import { htmx } from './htmx.ts';

function scriptNonces(policy: string) {
    const directives = new Map<string, string[]>();
    for (const directive of policy.split(';')) {
        const [name, ...sources] = directive.match(/[^\t\n\f\r ]+/g) ?? [];
        if (!name) continue;
        const key = name.toLowerCase();
        if (!directives.has(key)) directives.set(key, sources);
    }
    const sources = directives.get('script-src-elem') ?? directives.get('script-src') ?? directives.get('default-src');
    if (!sources) return null;

    const nonces = new Set<string>();
    for (const source of sources) {
        const value = /^'nonce-([a-z0-9+/_-]+={0,2})'$/i.exec(source)?.[1];
        if (value) nonces.add(value);
    }
    return nonces;
}

function responseNonces(xhr: XMLHttpRequest) {
    const url = xhr.responseURL && URL.parse(xhr.responseURL, window.location.href);
    if (!url || url.origin !== window.location.origin) return new Set<string>();

    const header = xhr.getResponseHeader('HX-Nonce');
    let allowed = header ? new Set([header]) : null;
    const csp = xhr.getResponseHeader('Content-Security-Policy') ?? '';
    for (const policy of csp.split(',')) {
        const nonces = scriptNonces(policy);
        if (nonces) allowed = allowed ? allowed.intersection(nonces) : nonces;
    }
    return allowed ?? new Set<string>();
}

function filterResponse(doc: Document, nonces: Set<string>) {
    const roots: ParentNode[] = [doc];
    for (const root of roots) {
        for (const script of root.querySelectorAll('script')) {
            const nonce = script.getAttribute('nonce');
            if (!nonce?.trim() || !nonces.has(nonce)) script.remove();
        }
        for (const attribute of ['hx-ext', 'data-hx-ext']) {
            for (const element of root.querySelectorAll(`[${attribute}]`)) {
                // Named form controls can shadow both attribute methods.
                const tokens = Element.prototype.getAttribute.call(element, attribute)!.split(',');
                // HTMX ignores ASCII spaces in each extension name.
                const kept = tokens.filter((token) => token.replaceAll(' ', '') !== 'ignore:safe-nonce');
                if (kept.length !== tokens.length)
                    Element.prototype.setAttribute.call(element, attribute, kept.join(','));
            }
        }
        // HTMX can activate scripts from template contents after the response is filtered.
        for (const template of root.querySelectorAll('template')) {
            if (template instanceof HTMLTemplateElement) roots.push(template.content);
        }
    }
}

htmx.defineExtension('safe-nonce', {
    transformResponse: function (text, xhr) {
        try {
            const nonces = responseNonces(xhr);
            const doc = new DOMParser().parseFromString(text, 'text/html');
            filterResponse(doc, nonces);
            return doc.documentElement.outerHTML;
        } catch {
            // HTMX continues with the original response if an extension throws.
            return '';
        }
    },
});
