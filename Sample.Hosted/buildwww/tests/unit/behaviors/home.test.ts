import assert from 'node:assert/strict';
import test from 'node:test';
import type { HelloReactProps } from '../../../lib/react/components/HelloReact.tsx';
import utils from '../../../app/ts/drn/drnUtils.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { behaviorRegistry } from './behaviorRegistry.ts';

test('home island behavior', async (t) => {
    const registry = behaviorRegistry();
    const document = Object.assign(new EventTarget(), { readyState: 'loading' });
    const scan = t.mock.fn();
    const classes = new Set<string>();
    const badge = {
        classList: { add: (name: string) => classes.add(name), remove: (name: string) => classes.delete(name) },
    };
    class Host {
        dataset = { versions: '' };
        shadowRoot = { querySelector: (selector: string) => (selector === '.hello-react-badge' ? badge : {}) };
    }
    const host = new Host();
    const update = t.mock.fn();
    const dispose = t.mock.fn();
    const island = { getProps: () => props, update, dispose };
    let available = false;
    let props!: HelloReactProps;
    const mount = (name: string, element: Host, initial: HelloReactProps) => {
        assert.equal(name, 'HelloReact');
        assert.equal(element, host);
        props = initial;
        return available ? island : null;
    };
    installBrowserGlobals(t, {
        document,
        window: { onmount: scan },
        HTMLElement: Host,
        DRN: { Onmount: registry, React: { mount }, Utils: utils },
        getComputedStyle: () => ({ getPropertyValue: (name: string) => (name === '--badge-fade-ms' ? '5' : '20') }),
    });
    const log = t.mock.method(console, 'log', () => {});
    await import('../../../app/ts/homeBehaviors.ts');
    document.dispatchEvent(new Event('DOMContentLoaded'));
    document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.equal(scan.mock.callCount(), 1);
    const selector = '[data-js-hello-react-island]';
    await t.test('rejects malformed version data and keeps only recognized fields', () => {
        for (const raw of ['', '{', '[]', '{"react":"19","vite":7}']) {
            host.dataset.versions = raw;
            assert.equal(registry.mount(selector, host).result, false);
            assert.deepEqual(props.versions, {});
        }
        host.dataset.versions = '{"react":"19","extra":"ignored"}';
        assert.equal(registry.mount(selector, host).result, false);
        assert.deepEqual(props.versions, { react: '19' });
    });
    await t.test('reports readiness and counts card clicks per mount', () => {
        registry.mount(selector, host);
        props.onReady?.();
        props.onCardClick?.();
        props.onCardClick?.();
        assert.deepEqual(
            log.mock.calls.slice(-3).map((call) => call.arguments[0]),
            [
                'HelloReact component is ready',
                'Card clicked! Total clicks outside the component: 1',
                'Card clicked! Total clicks outside the component: 2',
            ],
        );
    });
    await t.test('updates the title and cancels a pending transition on disposal', (t) => {
        available = true;
        t.mock.timers.enable({ apis: ['setInterval', 'setTimeout'] });
        const { disposable } = registry.mount(selector, host);
        assert.equal(props.title, 'Build with Confidence');
        t.mock.timers.tick(20);
        assert.equal(classes.has('is-transitioning'), true);
        t.mock.timers.tick(5);
        assert.deepEqual(update.mock.calls[0]?.arguments, [{ title: 'DDD & Mounted Islands Architecture' }]);
        assert.equal(classes.has('is-transitioning'), false);
        t.mock.timers.tick(15);
        assert.equal(classes.has('is-transitioning'), true);
        assert.ok(disposable);
        disposable.dispose?.();
        t.mock.timers.tick(100);
        assert.equal(update.mock.callCount(), 1);
        assert.equal(dispose.mock.callCount(), 1);
    });
});
