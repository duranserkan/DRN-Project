import assert from 'node:assert/strict';
import test from 'node:test';
import panel from '../../../app/ts/drn/drnNotificationPanel.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('HTMX cleanup of a panel being opened waits for Bootstrap to finish showing and hiding before disposal', (t) => {
    const id = 'offCanvasSidebarActionCanvasNotifications';
    class Element extends EventTarget {
        contains(node: Element) {
            return node === this;
        }
    }
    const classes = new Set<string>();
    const view = Object.assign(new Element(), {
        id,
        classList: { contains: (name: string) => classes.has(name) },
        matches: (selector: string) => selector.split(', ').some((name) => classes.has(name.slice(1))),
    });
    const document = Object.assign(new EventTarget(), {
        getElementById: (key: string) => (key === id ? view : null),
        querySelectorAll: () => [],
    });
    const instance = { hide: t.mock.fn(), dispose: t.mock.fn() };
    const store = { subscribe: t.mock.fn(), list: () => [], markSeen: t.mock.fn() };
    installBrowserGlobals(t, {
        Element,
        document,
        window: { DRN: { Notifications: store }, bootstrap: { Offcanvas: { getInstance: () => instance } } },
    });
    panel.install();
    classes.add('showing');
    const cleanup = (elt: Element) =>
        document.dispatchEvent(new CustomEvent('htmx:beforeCleanupElement', { detail: { elt } }));
    cleanup(new Element());
    view.dispatchEvent(new Event('shown.bs.offcanvas'));
    assert.equal(instance.hide.mock.callCount(), 0);
    cleanup(view);
    assert.equal(instance.hide.mock.callCount(), 0);
    assert.equal(instance.dispose.mock.callCount(), 0);
    view.dispatchEvent(new Event('shown.bs.offcanvas'));
    assert.equal(instance.hide.mock.callCount(), 1);
    assert.equal(instance.dispose.mock.callCount(), 0);
    view.dispatchEvent(new Event('hidden.bs.offcanvas'));
    view.dispatchEvent(new Event('hidden.bs.offcanvas'));
    assert.equal(instance.dispose.mock.callCount(), 1);
});
