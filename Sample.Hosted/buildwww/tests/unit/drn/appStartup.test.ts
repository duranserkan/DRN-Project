import { test, vi } from 'vitest';
import assert from 'node:assert/strict';
import { getEventListeners } from 'node:events';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('preload preserves the existing namespace, installs error listeners and supplies a fallback React mount', async () => {
    const namespace = { marker: 'existing' };
    const window = Object.assign(new EventTarget(), { DRN: namespace, location: new URL('https://example.test') });
    const document = new EventTarget();
    installBrowserGlobals({ window, document });
    await import('../../../app/ts/appPreload.ts');
    assert.equal(globalThis.window.DRN, namespace);
    assert.equal(namespace.marker, 'existing');
    assert.equal(globalThis.window.DRN.React.mount('HelloReact', null, { title: 'Pending' }), null);
    assert.equal(typeof globalThis.window.DRN.Problem.fromResponse, 'function');
    assert.equal(getEventListeners(window, 'error').length, 1);
    assert.equal(getEventListeners(window, 'unhandledrejection').length, 1);
});

test('postload scans immediately, once on DOM readiness, and after each HTMX load', async () => {
    const scan = vi.fn();
    const sync = vi.fn();
    const document = Object.assign(new EventTarget(), {
        readyState: 'loading',
        querySelector: () => null,
        querySelectorAll: () => [],
        getElementById: () => null,
    });
    const DRN = { Notifications: { sync, subscribe: vi.fn(), list: () => [] } };
    const window = Object.assign(new EventTarget(), { DRN, onmount: scan, setInterval: () => 1 });
    installBrowserGlobals({ window, document, DRN });
    await import('../../../app/ts/appPostload.ts');
    assert.equal(scan.mock.calls.length, 1);
    for (const type of ['DOMContentLoaded', 'DOMContentLoaded', 'htmx:load', 'htmx:load']) {
        document.dispatchEvent(new Event(type));
    }
    assert.equal(scan.mock.calls.length, 4);
    assert.equal(sync.mock.calls.length, 3);
});
