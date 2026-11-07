import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { loadTsx } from './loadTsx.ts';
import { islandDocument } from './islandDocument.ts';

test('a stale island handle cannot update or dispose the replacement shadow root', async (t) => {
    loadTsx(t);
    const lightRoot = { render: t.mock.fn(), unmount: t.mock.fn() };
    const shadowRoot = { render: t.mock.fn(), unmount: t.mock.fn() };
    const shadowContainer = {};
    const classes = new Set<string>();
    const host = {
        classList: { contains: (name: string) => classes.has(name), add: (name: string) => classes.add(name) },
        shadowRoot: { querySelector: (selector: string) => (selector === '#drn-portal-root' ? shadowContainer : {}) },
    } as unknown as HTMLElement;
    const createRoot = (container: unknown) => {
        if (container === host) return lightRoot;
        assert.equal(container, shadowContainer);
        return shadowRoot;
    };
    installBrowserGlobals(t, {
        window: { DRN: { React: {} } },
        document: islandDocument(),
        createTestRoot: createRoot,
    });
    const client = registerHooks({
        resolve(specifier, context, nextResolve) {
            if (specifier === 'react-dom/client') {
                return {
                    shortCircuit: true,
                    url: 'data:text/javascript,export const createRoot = globalThis.createTestRoot',
                };
            }
            return nextResolve(specifier, context);
        },
    });
    t.after(() => client.deregister());
    await import('../../../lib/react/reactBundle.tsx');
    t.mock.method(console, 'error', () => {});
    // @ts-expect-error A component name from untyped page markup can be unknown.
    assert.equal(window.DRN.React.mount('Missing', host, {}), null);
    assert.equal(lightRoot.render.mock.callCount(), 0);
    const previous = window.DRN.React.mount('HelloReact', host, { title: 'First' }, { useShadow: false });
    assert.ok(classes.has('drn-react-root'));
    const reused = window.DRN.React.mount('HelloReact', host, { title: 'Updated' }, { useShadow: false });
    assert.deepEqual(previous?.getProps(), { title: 'Updated' });
    assert.deepEqual(reused?.getProps(), previous?.getProps());
    assert.equal(lightRoot.unmount.mock.callCount(), 0);
    const replacement = window.DRN.React.mount('HelloReact', host, { title: 'Second' }, { useShadow: true });
    assert.ok(previous && replacement);
    assert.equal(lightRoot.unmount.mock.callCount(), 1);
    previous.update({ title: 'Stale' });
    previous.dispose();
    assert.equal(previous.getProps(), null);
    assert.deepEqual(replacement.getProps(), { title: 'Second' });
    assert.equal(lightRoot.render.mock.callCount(), 2);
    assert.equal(lightRoot.unmount.mock.callCount(), 1);
    assert.equal(shadowRoot.render.mock.callCount(), 1);
    assert.equal(shadowRoot.unmount.mock.callCount(), 0);
    replacement.update({ title: 'Current' });
    assert.deepEqual(replacement.getProps(), { title: 'Current' });
    assert.equal(shadowRoot.render.mock.callCount(), 2);
    replacement.dispose();
    assert.equal(shadowRoot.unmount.mock.callCount(), 1);
});
