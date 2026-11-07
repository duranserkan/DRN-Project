import assert from 'node:assert/strict';
import test from 'node:test';
import drnOnmount from '../../../app/ts/drn/drnOnmount.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('invalid registrations reserve no keys and the default cleanup supports both disposable methods', (t) => {
    const before = drnOnmount.listRegistrationKeys();
    const callback = t.mock.fn();
    t.mock.method(console, 'error', () => {});
    drnOnmount.registerFull('  ', callback);
    // @ts-expect-error A plain JavaScript caller can omit the mount function.
    drnOnmount.registerFull('.invalid', null);
    // @ts-expect-error Registration keys supplied by untyped bundles may be invalid.
    drnOnmount.registerFull('.invalid', callback, undefined, 42);
    assert.deepEqual(drnOnmount.listRegistrationKeys(), before);
    const dispose = t.mock.fn();
    const destroy = t.mock.fn();
    drnOnmount.unregister.call({} as Element, { disposable: { dispose, destroy } });
    assert.equal(dispose.mock.callCount(), 1);
    assert.equal(destroy.mock.callCount(), 1);
});

test('loading documents register each selector and key once on DOMContentLoaded', (t) => {
    const document = Object.assign(new EventTarget(), { readyState: 'loading', body: null });
    const registrations: unknown[][] = [];
    const onmount = (...args: unknown[]) => {
        if (args.length) registrations.push(args);
    };
    installBrowserGlobals(t, { document, onmount, window: { onmount } });
    const selector = '[data-deferred-widget]';
    const first = t.mock.fn();
    const second = t.mock.fn();
    drnOnmount.register(selector, first, 'first');
    drnOnmount.register(selector, first, 'first');
    drnOnmount.register(selector, second, 'second');
    assert.equal(registrations.length, 0);
    document.dispatchEvent(new Event('DOMContentLoaded'));
    document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.deepEqual(
        registrations.map(([target, callback]) => [target, callback]),
        [
            [selector, first],
            [selector, second],
        ],
    );
});

test('duplicate registration waits until after a body swap to scan for mounts', (t) => {
    const classes = new Set(['htmx-swapping']);
    const calls: unknown[][] = [];
    const onmount = (...args: unknown[]) => calls.push(args);
    installBrowserGlobals(t, {
        document: { readyState: 'complete', body: { classList: { contains: (name: string) => classes.has(name) } } },
        window: { onmount },
        onmount,
    });

    const selector = '[data-test-widget]';
    const mount = t.mock.fn();
    drnOnmount.register(selector, mount, 'widget');
    drnOnmount.register(selector, mount, 'widget');
    assert.equal(calls.length, 1);
    assert.equal(calls[0]?.[0], selector);
    assert.equal(calls[0]?.[1], mount);

    classes.delete('htmx-swapping');
    drnOnmount.register(selector, mount, 'widget');
    assert.equal(calls.length, 2);
    assert.deepEqual(calls[1], []);
});
