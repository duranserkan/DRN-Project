import assert from 'node:assert/strict';
import test from 'node:test';
import { getInlineStyleNonce } from '../../../lib/react/nonceHelper.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('nonce discovery retries after a miss and retains the first usable DOM nonce', (t) => {
    assert.equal(getInlineStyleNonce(), '');
    const meta = { content: '{', getAttribute: () => null };
    const script = { nonce: '', getAttribute: () => null };
    const elements = new Map<string, typeof meta | typeof script>([
        ['meta[name="htmx-config"]', meta],
        ['script[nonce], style[nonce]', script],
    ]);
    installBrowserGlobals(t, { document: { querySelector: (selector: string) => elements.get(selector) }, window: {} });
    assert.equal(getInlineStyleNonce(), '');
    assert.equal(window.__webpack_nonce__, undefined);
    script.nonce = 'document-token';
    assert.equal(getInlineStyleNonce(), 'document-token');
    assert.equal(window.__webpack_nonce__, 'document-token');
    elements.clear();
    assert.equal(getInlineStyleNonce(), 'document-token');
});
