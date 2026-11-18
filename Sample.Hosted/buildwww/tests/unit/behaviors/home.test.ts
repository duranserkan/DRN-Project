import { onTestFinished, test, vi } from 'vitest';
import assert from 'node:assert/strict';
import type { HelloReactProps } from '../../../lib/react/components/HelloReact.tsx';
import utils from '../../../app/ts/drn/drnUtils.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';
import { behaviorRegistry } from './behaviorRegistry.ts';

async function setup(available = false) {
    const registry = behaviorRegistry();
    const document = Object.assign(new EventTarget(), { readyState: 'loading' });
    const scan = vi.fn();
    const classes = new Set<string>();
    const badge = {
        classList: { add: (name: string) => classes.add(name), remove: (name: string) => classes.delete(name) },
    };
    class Host {
        dataset = { versions: '' };
        shadowRoot = { querySelector: (selector: string) => (selector === '.hello-react-badge' ? badge : {}) };
    }
    const host = new Host();
    const update = vi.fn();
    const dispose = vi.fn();
    const island = { getProps: () => props, update, dispose };
    let props!: HelloReactProps;
    const mount = (name: string, element: Host, initial: HelloReactProps) => {
        assert.equal(name, 'HelloReact');
        assert.equal(element, host);
        props = initial;
        return available ? island : null;
    };
    installBrowserGlobals({
        document,
        window: { onmount: scan },
        HTMLElement: Host,
        DRN: { Onmount: registry, React: { mount }, Utils: utils },
        getComputedStyle: () => ({ getPropertyValue: (name: string) => (name === '--badge-fade-ms' ? '5' : '20') }),
    });
    const log = vi.spyOn(console, 'log').mockImplementation(() => {});
    vi.resetModules();
    await import('../../../app/ts/homeBehaviors.ts');
    document.dispatchEvent(new Event('DOMContentLoaded'));
    document.dispatchEvent(new Event('DOMContentLoaded'));
    assert.equal(scan.mock.calls.length, 1);
    return {
        host,
        classes,
        log,
        update,
        dispose,
        mount: () => registry.mount('[data-js-hello-react-island]', host),
        getProps: () => props,
    };
}

test('home island rejects malformed version data and keeps only recognized fields', async () => {
    const { host, mount, getProps } = await setup();
    for (const raw of ['', '{', '[]', '{"react":"19","vite":7}']) {
        host.dataset.versions = raw;
        assert.equal(mount().result, false);
        assert.deepEqual(getProps().versions, {});
    }
    host.dataset.versions = '{"react":"19","extra":"ignored"}';
    assert.equal(mount().result, false);
    assert.deepEqual(getProps().versions, { react: '19' });
});

test('home island reports readiness and counts card clicks per mount', async () => {
    const { mount, getProps, log } = await setup();
    mount();
    const props = getProps();
    props.onReady?.();
    props.onCardClick?.();
    props.onCardClick?.();
    assert.deepEqual(
        log.mock.calls.slice(-3).map((call) => call[0]),
        [
            'HelloReact component is ready',
            'Card clicked! Total clicks outside the component: 1',
            'Card clicked! Total clicks outside the component: 2',
        ],
    );
});

test('home island updates the title and cancels a pending transition on disposal', async () => {
    const { mount, getProps, classes, update, dispose } = await setup(true);
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'setTimeout', 'clearTimeout'] });
    onTestFinished(() => {
        vi.useRealTimers();
    });
    const { disposable } = mount();
    assert.equal(getProps().title, 'Build with Confidence');
    vi.advanceTimersByTime(20);
    assert.equal(classes.has('is-transitioning'), true);
    vi.advanceTimersByTime(5);
    assert.deepEqual(update.mock.calls[0], [{ title: 'DDD & Mounted Islands Architecture' }]);
    assert.equal(classes.has('is-transitioning'), false);
    vi.advanceTimersByTime(15);
    assert.equal(classes.has('is-transitioning'), true);
    assert.ok(disposable);
    disposable.dispose?.();
    vi.advanceTimersByTime(100);
    assert.equal(update.mock.calls.length, 1);
    assert.equal(dispose.mock.calls.length, 1);
});
