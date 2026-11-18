import { test, vi } from 'vitest';
import assert from 'node:assert/strict';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { islandDocument } from './islandDocument.ts';

test('a stale island handle cannot update or dispose the replacement shadow root', async () => {
    const lightRoot = { render: vi.fn(), unmount: vi.fn() };
    const shadowRoot = { render: vi.fn(), unmount: vi.fn() };
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
    installBrowserGlobals({
        window: { DRN: { React: {} } },
        document: islandDocument(),
    });
    vi.doMock('react-dom/client', () => ({ createRoot }));
    await import('../../../lib/react/reactBundle.tsx');
    vi.spyOn(console, 'error').mockImplementation(() => {});
    // @ts-expect-error A component name from untyped page markup can be unknown.
    assert.equal(window.DRN.React.mount('Missing', host, {}), null);
    assert.equal(lightRoot.render.mock.calls.length, 0);
    const previous = window.DRN.React.mount('HelloReact', host, { title: 'First' }, { useShadow: false });
    assert.ok(classes.has('drn-react-root'));
    const reused = window.DRN.React.mount('HelloReact', host, { title: 'Updated' }, { useShadow: false });
    assert.deepEqual(previous?.getProps(), { title: 'Updated' });
    assert.deepEqual(reused?.getProps(), previous?.getProps());
    assert.equal(lightRoot.unmount.mock.calls.length, 0);
    const replacement = window.DRN.React.mount('HelloReact', host, { title: 'Second' }, { useShadow: true });
    assert.ok(previous && replacement);
    assert.equal(lightRoot.unmount.mock.calls.length, 1);
    previous.update({ title: 'Stale' });
    previous.dispose();
    assert.equal(previous.getProps(), null);
    assert.deepEqual(replacement.getProps(), { title: 'Second' });
    assert.equal(lightRoot.render.mock.calls.length, 2);
    assert.equal(lightRoot.unmount.mock.calls.length, 1);
    assert.equal(shadowRoot.render.mock.calls.length, 1);
    assert.equal(shadowRoot.unmount.mock.calls.length, 0);
    replacement.update({ title: 'Current' });
    assert.deepEqual(replacement.getProps(), { title: 'Current' });
    assert.equal(shadowRoot.render.mock.calls.length, 2);
    replacement.dispose();
    assert.equal(shadowRoot.unmount.mock.calls.length, 1);
});
