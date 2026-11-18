import { test, vi } from 'vitest';
import assert from 'node:assert/strict';
import { installActionbarEvents } from '../../../app/ts/drn/drnActionbar.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

function actionbar() {
    const marker = { dataset: { notificationApp: '/', notificationOwner: 'alice' } };
    const badge = { textContent: ' 2 ' };
    const classes = new Set<string>();
    const control = {
        dataset: { bsTarget: '#messages', actionbarAttentionVersion: '' },
        querySelector: () => badge,
        classList: {
            toggle: (name: string, enabled: boolean) => (enabled ? classes.add(name) : classes.delete(name)),
            remove: (name: string) => classes.delete(name),
        },
    };
    const document = Object.assign(new EventTarget(), {
        readyState: 'loading',
        querySelector: () => marker,
        querySelectorAll: () => [control],
    });
    const window = new EventTarget();
    const saved = new Map<string, string>();
    const storage = {
        getItem: (key: string) => saved.get(key) ?? null,
        setItem: (k: string, v: string) => saved.set(k, v),
    };
    installBrowserGlobals({ document, window, localStorage: storage });
    installActionbarEvents();
    return {
        document,
        window,
        marker,
        control,
        badge,
        storage,
        saved,
        highlighted: () => classes.has('text-bg-warning'),
        open() {
            const event = new Event('shown.bs.offcanvas');
            Object.defineProperty(event, 'target', { value: { id: 'messages' } });
            document.dispatchEvent(event);
        },
    };
}

test('actionbar acknowledgments follow the attention version and belong to the current account', () => {
    const bar = actionbar();
    bar.document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.equal(bar.highlighted(), true);
    bar.open();
    assert.equal(bar.highlighted(), false);
    assert.equal(bar.badge.textContent.trim(), '2');
    bar.marker.dataset.notificationOwner = 'bob';
    bar.document.dispatchEvent(new Event('drn:notification-context-changed'));
    assert.equal(bar.highlighted(), true);
    bar.marker.dataset.notificationOwner = 'alice';
    bar.window.dispatchEvent(new Event('pageshow'));
    assert.equal(bar.highlighted(), false);
    bar.control.dataset.actionbarAttentionVersion = 'new-message';
    bar.document.dispatchEvent(new Event('htmx:load'));
    assert.equal(bar.highlighted(), true);
});

test('actionbar restores legacy attendance and retains failed writes only within the active account', () => {
    const bar = actionbar();
    bar.saved.set('drn.actionbar.v2:["/","alice"]', JSON.stringify({ owner: 'alice', entries: [['#messages', '2']] }));
    bar.document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.equal(bar.highlighted(), false);
    bar.badge.textContent = '3';
    bar.document.dispatchEvent(new Event('htmx:load'));
    assert.equal(bar.highlighted(), true);
    vi.spyOn(bar.storage, 'setItem').mockImplementation(() => {
        throw new Error('storage blocked');
    });
    bar.open();
    bar.window.dispatchEvent(Object.assign(new Event('storage'), { key: null }));
    assert.equal(bar.highlighted(), false);
    bar.marker.dataset.notificationOwner = 'bob';
    bar.document.dispatchEvent(new Event('drn:notification-context-changed'));
    assert.equal(bar.highlighted(), true);
});
