import assert from 'node:assert/strict';
import test from 'node:test';
import { behaviorRegistry } from './behaviorRegistry.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('layout skips incomplete consent forms and registers a disposable tooltip', async (t) => {
    const registry = behaviorRegistry();
    const scan = t.mock.fn();
    const document = Object.assign(new EventTarget(), { readyState: 'loading' });
    class Host {
        dataset = { preferencesCookieName: ' ' };
        querySelector() {
            return null;
        }
    }
    const host = new Host();
    const dispose = t.mock.fn();
    class Tooltip {
        dispose = dispose;
        constructor(element: Host, options: object) {
            assert.equal(element, host);
            assert.deepEqual(options, { animation: false });
        }
    }
    installBrowserGlobals(t, {
        window: { onmount: scan },
        document,
        HTMLElement: Host,
        HTMLInputElement: class {},
        bootstrap: { Tooltip },
        DRN: { Onmount: registry },
    });
    const warn = t.mock.method(console, 'warn', () => {});
    await import('../../../app/ts/layoutBehaviors.ts');
    document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.equal(scan.mock.callCount(), 1);
    const footer = '[data-js-drn-footer]';
    assert.equal(registry.mount(footer, host).disposable, undefined);
    assert.match(String(warn.mock.calls[0]?.arguments[0]), /cookie-name is missing or blank/);
    host.dataset.preferencesCookieName = 'preferences';
    assert.equal(registry.mount(footer, host).disposable, undefined);
    assert.match(String(warn.mock.calls[1]?.arguments[0]), /expected a navbar/);
    const { disposable } = registry.mount('[data-bs-toggle="tooltip"]', host);
    assert.ok(disposable instanceof Tooltip);
    disposable.dispose();
    assert.equal(dispose.mock.callCount(), 1);
});

test('the exception demo throws on click and removes the same listener on disposal', async (t) => {
    const registry = behaviorRegistry();
    const document = Object.assign(new EventTarget(), { readyState: 'loading' });
    const scan = t.mock.fn();
    installBrowserGlobals(t, { DRN: { Onmount: registry }, document, window: { onmount: scan } });
    await import('../../../app/ts/testBehaviors.ts');
    document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.equal(scan.mock.callCount(), 1);
    const listeners = new Set<() => void>();
    const button = {
        addEventListener(type: string, fn: () => void) {
            assert.equal(type, 'click');
            listeners.add(fn);
        },
        removeEventListener(type: string, fn: () => void) {
            assert.equal(type, 'click');
            listeners.delete(fn);
        },
    };
    const { disposable } = registry.mount('[data-js-unhandled-browser-exception]', button);
    assert.equal(listeners.size, 1);
    for (const click of listeners) assert.throws(click, /Intentional unhandled browser exception/);
    assert.ok(disposable);
    disposable.dispose?.();
    assert.equal(listeners.size, 0);
});
