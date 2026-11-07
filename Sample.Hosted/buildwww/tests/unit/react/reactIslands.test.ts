import assert from 'node:assert/strict';
import test from 'node:test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { loadTsx } from './loadTsx.ts';

test('React islands skip mounting without a host, render props, and reset errors when the key changes', async (t) => {
    loadTsx(t);
    Object.defineProperty(globalThis, 'window', { configurable: true, value: { DRN: { React: {} } } });
    t.after(() => Reflect.deleteProperty(globalThis, 'window'));
    await import('../../../lib/react/reactBundle.tsx');
    assert.equal(window.DRN.React.mount('HelloReact', null, { title: 'Sample' }), null);

    const { HelloReact } = await import('../../../lib/react/components/HelloReact.tsx');
    const html = renderToStaticMarkup(createElement(HelloReact, { title: 'Sample island', versions: { react: 'X' } }));
    assert.match(html, /Sample island/);
    assert.match(html, /React X/);

    const { IslandPortal } = await import('../../../lib/react/PortalContext.tsx');
    assert.equal(renderToStaticMarkup(createElement(IslandPortal, null, 'Hidden without a portal')), '');

    const { IslandErrorBoundary } = await import('../../../lib/react/IslandErrorBoundary.tsx');
    const boundary = new IslandErrorBoundary({ children: 'Ready', resetKey: 'first' });
    assert.equal(boundary.render(), 'Ready');
    boundary.state = { ...boundary.state, ...IslandErrorBoundary.getDerivedStateFromError() };
    assert.match(renderToStaticMarkup(boundary.render()), /Failed to load component/);
    assert.deepEqual(
        IslandErrorBoundary.getDerivedStateFromProps({ children: 'Ready', resetKey: 'next' }, boundary.state),
        {
            hasError: false,
            prevResetKey: 'next',
        },
    );
});
