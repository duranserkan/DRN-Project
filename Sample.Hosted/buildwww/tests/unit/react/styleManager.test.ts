import assert from 'node:assert/strict';
import test from 'node:test';
import { ensureDocumentStyles, initSharedStyleSheet, resolveMountContainer } from '../../../lib/react/styleManager.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { islandDocument, IslandNode } from './islandDocument.ts';

test('a rejected shared sheet falls back to nonce-bearing styles once per document and shadow root', (t) => {
    class UnsupportedSheet {
        replaceSync() {
            throw new Error('unsupported');
        }
    }
    const document = {
        ...islandDocument(),
        querySelector: () => ({
            getAttribute: () => null,
            content: JSON.stringify({ inlineStyleNonce: 'style-token', inlineScriptNonce: 'script-token' }),
        }),
    };
    installBrowserGlobals(t, {
        document,
        window: { CSSStyleSheet: UnsupportedSheet },
        CSSStyleSheet: UnsupportedSheet,
    });
    t.mock.method(console, 'warn', () => {});
    const css = '.island { color: red }';
    assert.equal(initSharedStyleSheet(css), null);
    const host = new IslandNode();
    const element = host as unknown as HTMLElement;
    const portal = resolveMountContainer(element, true, css);
    assert.equal(resolveMountContainer(element, true, css), portal);
    assert.equal(document.head.children.length, 1);
    assert.equal(host.shadowRoot?.children.length, 2);
    assert.equal(host.shadowRoot?.querySelector('#drn-portal-root'), portal);
    for (const style of [document.head.children[0], host.shadowRoot?.querySelector('#drn-shadow-dom-styles')]) {
        assert.equal(style?.textContent, css);
        assert.equal(style?.nonce, 'style-token');
        assert.equal(style?.attributes.get('nonce'), 'style-token');
    }
    assert.equal(window.__webpack_nonce__, 'style-token');
});

test('adopting the shared sheet twice preserves existing document styles', (t) => {
    class Sheet {
        css = '';
        replaceSync(css: string) {
            this.css = css;
        }
    }
    const existing = new Sheet();
    const document = { ...islandDocument(), adoptedStyleSheets: [existing] };
    installBrowserGlobals(t, { window: { CSSStyleSheet: Sheet }, CSSStyleSheet: Sheet, document });
    const css = '.island { color: green }';
    const shared = initSharedStyleSheet(css);
    assert.ok(shared instanceof Sheet);
    assert.equal(shared.css, css);
    ensureDocumentStyles(css);
    ensureDocumentStyles(css);
    assert.deepEqual(document.adoptedStyleSheets, [existing, shared]);
    const host = new IslandNode();
    const shadow = host.attachShadow();
    shadow.adoptedStyleSheets = [existing as unknown as CSSStyleSheet];
    const element = host as unknown as HTMLElement;
    assert.equal(resolveMountContainer(element, true, css), resolveMountContainer(element, true, css));
    assert.deepEqual(shadow.adoptedStyleSheets, [existing, shared]);
    assert.equal(shadow.querySelector('#drn-shadow-dom-styles'), undefined);
});
