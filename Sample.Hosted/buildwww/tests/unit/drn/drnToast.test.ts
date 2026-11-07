import assert from 'node:assert/strict';
import test from 'node:test';
import toast from '../../../app/ts/drn/drnToast.ts';
import utils from '../../../app/ts/drn/drnUtils.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

test('toast bursts retain history, evict the oldest visible toast and recover at exactly ten seconds', (t) => {
    let now = 0;
    t.mock.method(Date, 'now', () => now);
    t.mock.method(utils, 'escapeHtml', (text: string) => text);
    const published: string[] = [];
    const shown: string[] = [];
    const visible = new Set<string>();
    const live = { textContent: '' };
    const elements = new Map<string, EventTarget>();
    class Toast {
        message = live.textContent;
        element: EventTarget;
        constructor(element: EventTarget) {
            this.element = element;
        }
        show() {
            elements.set(this.message, this.element);
            shown.push(this.message);
            visible.add(this.message);
            assert.ok(visible.size <= 3);
        }
        hide() {
            visible.delete(this.message);
            this.element.dispatchEvent(new Event('hidden.bs.toast'));
        }
        dispose() {
            visible.delete(this.message);
        }
    }
    installBrowserGlobals(t, {
        window: { DRN: { Notifications: { publish: ({ message }: { message: string }) => published.push(message) } } },
        bootstrap: { Toast },
        document: {
            getElementById: () => live,
            querySelector: () => null,
            body: { contains: () => true, appendChild: t.mock.fn() },
            createElement: () =>
                Object.assign(new EventTarget(), {
                    style: {},
                    dataset: {},
                    setAttribute: t.mock.fn(),
                    appendChild: t.mock.fn(),
                    remove: t.mock.fn(),
                    contains: () => false,
                }),
        },
    });
    const burst = ['one', 'two', 'three', 'four', 'five', 'six'];
    toast.success('one');
    toast.warning('two');
    toast.error('three');
    for (const message of burst.slice(3)) toast.info(message);
    assert.deepEqual(published, burst);
    assert.deepEqual(shown, burst.slice(0, 5));
    assert.deepEqual([...visible], ['three', 'four', 'five']);
    now = 9999;
    toast.info('early');
    assert.deepEqual(shown, burst.slice(0, 5));
    now = 10000;
    toast.info('ready');
    assert.deepEqual(shown, [...burst.slice(0, 5), 'ready']);
    assert.deepEqual([...visible], ['four', 'five', 'ready']);
    assert.deepEqual(published, [...burst, 'early', 'ready']);
    toast.cleanup({ contains: (el: EventTarget) => el === elements.get('five') } as unknown as Element);
    assert.deepEqual([...visible], ['four', 'ready']);
    toast.cleanup(elements.get('ready') as Element);
    assert.deepEqual([...visible], ['four']);
    assert.deepEqual(published, [...burst, 'early', 'ready']);
});
