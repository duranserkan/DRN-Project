import assert from 'node:assert/strict';
import test, { type TestContext } from 'node:test';
import panel from '../../../app/ts/drn/drnNotificationPanel.ts';
import { installBrowserGlobals } from '../browserGlobals.ts';

class PanelControl extends EventTarget {
    dataset: Record<string, string> = {};
    attributes = new Map<string, string>();
    children: (PanelControl | string)[] = [];
    style = {};
    textContent = '';
    hidden = false;
    disabled = false;
    value = '';
    classList = { toggle() {}, remove() {}, add() {} };
    setAttribute(name: string, value: string) {
        this.attributes.set(name, value);
    }
    append(...children: (PanelControl | string)[]) {
        this.children.push(...children);
    }
    replaceChildren(child: PanelControl) {
        this.children = [...child.children];
    }
    get childElementCount() {
        return this.children.filter((child) => child instanceof PanelControl).length;
    }
    querySelectorAll() {
        return [];
    }
    contains() {
        return false;
    }
    closest(selector: string) {
        return selector === 'button' ? this : null;
    }
    focus() {}
    remove() {}
    *descendants(): Generator<PanelControl> {
        for (const child of this.children) {
            if (child instanceof PanelControl) {
                yield child;
                yield* child.descendants();
            }
        }
    }
}

function fixture(t: TestContext) {
    const list = new PanelControl();
    const search = new PanelControl();
    const result = new PanelControl();
    const undo = new PanelControl();
    const clearFiltered = new PanelControl();
    const clearAll = new PanelControl();
    const live = new PanelControl();
    const open = new PanelControl();
    const badge = new PanelControl();
    const triggerControls = new Map([
        ['[data-notification-open]', open],
        ['[data-notification-count]', badge],
    ]);
    const trigger = { querySelector: (selector: string) => triggerControls.get(selector) ?? null };
    const controls = new Map([
        ['#drn-notification-list', list],
        ['#drn-notification-search', search],
        ['#drn-notification-result', result],
        ['[data-notification-clear-all]', clearAll],
        ['[data-notification-clear-filtered]', clearFiltered],
        ['[data-notification-undo-region]', undo],
    ]);
    const filter = Object.assign(new PanelControl(), { dataset: { notificationFilter: 'error' } });
    const view = {
        matches: () => true,
        contains: () => true,
        querySelector: (selector: string) => controls.get(selector) ?? null,
        querySelectorAll: (selector: string) => (selector === '[data-notification-filter]' ? [filter] : []),
    };
    const elementsById = new Map<string, PanelControl | typeof view>([
        ['offCanvasSidebarActionCanvasNotifications', view],
        ['drn-notification-search', search],
        ['drn-notification-polite', live],
    ]);
    const document = Object.assign(new EventTarget(), {
        activeElement: undefined,
        querySelectorAll: (selector: string) => (selector === '[data-notification-trigger]' ? [trigger] : []),
        getElementById: (id: string) => elementsById.get(id) ?? null,
        createElement: () => new PanelControl(),
        createDocumentFragment: () => new PanelControl(),
    });
    const error = {
        id: 'first',
        type: 'error',
        title: 'Save failed',
        message: 'Retry',
        operation: 'Save',
        createdAt: 1,
        requestId: 'server-1',
        clientErrorId: 'client-1',
        clientErrorDetails: 'Message: <b>failed</b>\nLine: 4, column: 2\nStack: trace',
    };
    const original = [error, { ...error, id: 'second', requestId: 'server-2' }, { ...error, id: 'info', type: 'info' }];
    let entries = [...original];
    const store = {
        list: () => entries,
        subscribe: t.mock.fn(),
        dismiss(ids: string[]) {
            entries = entries.filter((entry) => !ids.includes(entry.id));
        },
        clear() {
            const removed = entries;
            entries = [];
            return removed;
        },
        restore(restored: typeof entries) {
            entries.push(...restored);
        },
    };
    installBrowserGlobals(t, { Element: PanelControl, document, window: { DRN: { Notifications: store } } });
    function click(control: PanelControl) {
        const event = new Event('click');
        Object.defineProperty(event, 'target', { value: control });
        document.dispatchEvent(event);
    }
    return { list, result, undo, clearAll, clearFiltered, live, filter, store, click, open, badge };
}

test('group details are built on expansion and retain occurrence-specific diagnostic references', (t) => {
    const view = fixture(t);
    panel.refresh(true);
    assert.equal(view.result.textContent, '3 notifications in 2 groups. Newest first.');
    const group = view.list.children[0];
    assert.ok(group instanceof PanelControl);
    const references = () => [...group.descendants()].flatMap((node) => node.dataset.notificationReference ?? []);
    assert.deepEqual(references(), []);
    const toggle = [...group.descendants()].find((node) => node.dataset.notificationAction === 'details');
    assert.ok(toggle);
    toggle.dispatchEvent(new Event('click'));
    assert.equal(toggle.attributes.get('aria-expanded'), 'true');
    assert.deepEqual(references(), ['server-1', 'client-1', 'server-2', 'client-1']);
    assert.ok([...group.descendants()].some((node) => node.children.includes(' <b>failed</b>')));
    toggle.dispatchEvent(new Event('click'));
    toggle.dispatchEvent(new Event('click'));
    assert.deepEqual(references(), ['server-1', 'client-1', 'server-2', 'client-1']);
});

test('filtered dismissal preserves other severities and undo restores only the subsequent clear', (t) => {
    const view = fixture(t);
    panel.install();
    panel.install();
    assert.equal(view.store.subscribe.mock.callCount(), 1);
    assert.equal(view.open.attributes.get('aria-label'), 'Notifications, 3 unseen');
    view.click(view.filter);
    assert.equal(view.filter.attributes.get('aria-pressed'), 'true');
    assert.equal(view.list.childElementCount, 1);
    assert.equal(view.filter.textContent, 'Errors (2)');
    view.clearFiltered.dataset.notificationClearFiltered = '';
    view.click(view.clearFiltered);
    assert.deepEqual(
        view.store.list().map((entry) => entry.id),
        ['info'],
    );
    assert.equal(view.result.textContent, 'No matching notifications.');
    assert.equal(view.clearFiltered.disabled, true);
    assert.equal(view.clearAll.disabled, false);
    view.clearAll.dataset.notificationClearAll = '';
    view.click(view.clearAll);
    assert.equal(view.result.textContent, 'No notifications.');
    assert.equal(view.badge.hidden, true);
    assert.equal(view.open.attributes.get('aria-label'), 'Notifications');
    assert.equal(view.undo.hidden, false);
    const undoButton = new PanelControl();
    undoButton.dataset.notificationUndo = '';
    view.click(undoButton);
    assert.deepEqual(
        view.store.list().map((entry) => entry.id),
        ['info'],
    );
    assert.equal(view.live.textContent, 'Notifications restored.');
    assert.equal(view.undo.hidden, true);
    assert.equal(view.badge.hidden, false);
    assert.equal(view.badge.textContent, '1');
});
