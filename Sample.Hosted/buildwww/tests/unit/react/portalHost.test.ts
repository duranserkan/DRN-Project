import assert from 'node:assert/strict';
import test from 'node:test';
import { getGlobalPortalContainer } from '../../../lib/react/portalHost.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { islandDocument, IslandNode } from './islandDocument.ts';

test('global portals reuse a connected container and recover after a body swap', (t) => {
    const document = islandDocument();
    installBrowserGlobals(t, { document });
    const first = getGlobalPortalContainer(null, '.menu {}', 'page-token');
    assert.equal(getGlobalPortalContainer(null, '.menu {}', 'page-token'), first);
    const shadow = document.getElementById('drn-global-portals')?.shadowRoot;
    assert.ok(shadow);
    assert.equal(shadow.querySelector('#portal-root'), first);
    const style = shadow.querySelector('#drn-portal-styles');
    assert.equal(style?.textContent, '.menu {}');
    assert.equal(style?.nonce, 'page-token');
    assert.equal(style?.attributes.get('nonce'), 'page-token');

    document.body.parent = null;
    document.body = islandDocument().body;
    assert.equal(first.isConnected, false);
    const existing = document.body.appendChild(new IslandNode());
    existing.id = 'drn-global-portals';
    const nextShadow = existing.attachShadow();
    const other = {} as CSSStyleSheet;
    const shared = {} as CSSStyleSheet;
    nextShadow.adoptedStyleSheets = [other];
    const second = getGlobalPortalContainer(shared, '.menu {}', '');
    assert.notEqual(second, first);
    assert.equal(second.isConnected, true);
    assert.equal(nextShadow.querySelector('#portal-root'), second);
    assert.deepEqual(nextShadow.adoptedStyleSheets, [other, shared]);
    assert.equal(nextShadow.querySelector('#drn-portal-styles'), undefined);
    assert.equal(document.body.children.length, 1);
});
