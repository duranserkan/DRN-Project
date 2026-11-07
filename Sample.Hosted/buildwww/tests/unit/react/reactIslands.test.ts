import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { registerHooks } from 'node:module';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { transformSync } from 'rolldown/utils';

test('React islands skip mounting without a host, render props, and reset errors when the key changes', async (t) => {
    // Load the production TSX under its original URL so Node can attribute hits to source lines.
    const hooks = registerHooks({
        load(url, context, nextLoad) {
            if (url.endsWith('.css?inline')) {
                const css = readFileSync(new URL(url), 'utf8');
                return { format: 'module', shortCircuit: true, source: `export default ${JSON.stringify(css)}` };
            }
            if (!url.endsWith('.tsx')) return nextLoad(url, context);
            const filename = fileURLToPath(url);
            const { code, map, errors } = transformSync(filename, readFileSync(filename, 'utf8'), {
                jsx: { runtime: 'automatic' },
                sourcemap: true,
            });
            if (errors.length) throw errors[0];
            assert.ok(map);
            const encoded = Buffer.from(JSON.stringify({ ...map, sources: [url] })).toString('base64');
            return {
                format: 'module',
                shortCircuit: true,
                source: `${code}\n//# sourceMappingURL=data:application/json;base64,${encoded}`,
            };
        },
    });
    t.after(() => hooks.deregister());
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
