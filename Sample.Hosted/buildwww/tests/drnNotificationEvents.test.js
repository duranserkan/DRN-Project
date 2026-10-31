import test from 'node:test';
import assert from 'node:assert/strict';
import { importStatusMessages, installNotificationEvents } from '../app/js/drn/drnNotificationEvents.js';
import { createNotificationStore } from '../app/js/drn/drnNotifications.js';
import problem from '../app/js/drn/drnProblem.js';

function statusElement(id, message, persist = true) {
    return {
        dataset: { notificationStatus: id },
        querySelector: () => ({ textContent: message }),
        closest: () => persist ? null : { dataset: { notificationPersist: 'false' } }
    };
}

function response(status = 200, contentType = 'text/html', headers = {}) {
    return {
        xhr: { status, responseText: '', getResponseHeader: name =>
            name === 'Content-Type' ? contentType : headers[name] ?? null },
        shouldSwap: status < 400,
        isError: status >= 400
    };
}

// Test our event policy with real EventTargets and store. Browser tests own DOM and HTMX integration.
function fixture(t, { start = true, statuses = [] } = {}) {
    const document = new EventTarget();
    const window = new EventTarget();
    const marker = { dataset: { notificationApp: '/app/', notificationOwner: 'account-a' } };
    document.readyState = 'loading';
    document.querySelector = selector => selector === '[data-notification-context]' ? marker : null;
    document.querySelectorAll = selector => selector === '[data-notification-status]' ? statuses : [];
    document.getElementById = () => null;
    document.body = {};
    const timers = new Set();
    const channels = new Set();
    window.setInterval = () => { const id = Symbol(); timers.add(id); return id; };
    window.clearInterval = id => timers.delete(id);
    class Channel {
        constructor() { channels.add(this); }
        postMessage() {}
        close() { channels.delete(this); }
    }
    let sequence = 0;
    const store = createNotificationStore({ storage: () => null, now: () => 100_000_000,
        newId: () => `event-${++sequence}` });
    const DRN = {
        Notifications: store,
        Problem: { ...problem, show: t.mock.fn(), clear: t.mock.fn(), clearAll: t.mock.fn() },
        Toast: { show: t.mock.fn(), cleanup: t.mock.fn() }
    };
    for (const [name, value] of Object.entries({ document, window, DRN, BroadcastChannel: Channel,
        location: new URL('https://sample.test/app/') })) {
        const original = Object.getOwnPropertyDescriptor(globalThis, name);
        Object.defineProperty(globalThis, name, { configurable: true, writable: true, value });
        t.after(() => {
            if (original) Object.defineProperty(globalThis, name, original);
            else delete globalThis[name];
        });
    }
    function emit(name, detail = {}) {
        const event = new Event(name, { cancelable: true });
        event.detail = detail;
        document.dispatchEvent(event);
        return event;
    }
    installNotificationEvents({ install() {}, refresh() {} });
    if (start) emit('DOMContentLoaded');
    return { emit, window, marker, statuses, store, timers, channels, ...DRN };
}

test('initial account binding preserves requests started before DOMContentLoaded', t => {
    const { emit, Problem } = fixture(t, { start: false });
    const detail = response();
    emit('htmx:beforeRequest', detail);
    emit('DOMContentLoaded');

    assert.equal(emit('htmx:beforeOnLoad', detail).defaultPrevented, false);
    emit('htmx:afterRequest', detail);
    assert.equal(Problem.clear.mock.callCount(), 1);
});

test('an account change rejects old responses before headers and still accepts new responses', t => {
    const { emit, marker, Problem, Toast } = fixture(t);
    const previous = response();
    emit('htmx:beforeRequest', previous);
    marker.dataset.notificationOwner = 'account-b';
    emit('htmx:load');

    assert.equal(emit('htmx:beforeOnLoad', previous).defaultPrevented, true);
    emit('showToast', { message: 'Previous account feedback' });
    emit('htmx:beforeSwap', previous);
    emit('htmx:responseError', previous);
    emit('htmx:afterRequest', previous);
    assert.equal(previous.shouldSwap, false);
    assert.equal(Problem.show.mock.callCount(), 0);
    assert.equal(Problem.clear.mock.callCount(), 0);
    assert.equal(Toast.show.mock.callCount(), 0);

    const current = response();
    emit('htmx:beforeRequest', current);
    assert.equal(emit('htmx:beforeOnLoad', current).defaultPrevented, false);
    emit('htmx:afterRequest', current);
    assert.equal(Problem.clear.mock.callCount(), 1);
});

test('cross-tab account changes suspend requests, untracked responses and status imports', t => {
    const { emit, channels, store, statuses, Problem } = fixture(t);
    store.publish({ message: 'Account A feedback' });
    const detail = response(500, 'application/problem+json');
    channels.values().next().value.onmessage({ data: { type: 'account', owner: 'account-b' } });
    statuses.push(statusElement('a'.repeat(32), 'Stale status'));
    emit('htmx:load');

    assert.equal(store.isActive(), false);
    assert.deepEqual(store.list(), []);
    assert.equal(statuses[0].dataset.notificationRecorded, undefined);
    assert.equal(emit('htmx:beforeRequest', response()).defaultPrevented, true);
    assert.equal(emit('htmx:beforeOnLoad', detail).defaultPrevented, true);
    emit('htmx:responseError', detail);
    assert.equal(Problem.show.mock.callCount(), 0);
    assert.equal(Problem.clearAll.mock.callCount(), 2);
});

test('store suspension also pauses the event bridge across lifecycle refreshes', t => {
    const { emit, window, store, Problem, Toast } = fixture(t);
    const pending = response(500, 'application/problem+json');
    emit('htmx:beforeRequest', pending);
    store.suspend();
    emit('htmx:load');
    window.dispatchEvent(new Event('pageshow'));

    assert.equal(store.isActive(), false);
    assert.equal(emit('htmx:beforeRequest', response()).defaultPrevented, true);
    assert.equal(emit('htmx:beforeOnLoad', pending).defaultPrevented, true);
    emit('showToast', { message: 'Suspended feedback' });
    emit('htmx:responseError', pending);
    emit('htmx:afterRequest', response());
    assert.equal(Problem.show.mock.callCount(), 0);
    assert.equal(Problem.clear.mock.callCount(), 0);
    assert.equal(Toast.show.mock.callCount(), 0);
});

test('restoring a page recreates one timer and channel without replaying inline status', t => {
    const { emit, window, channels, timers, store } = fixture(t, {
        statuses: [statusElement('a'.repeat(32), 'Profile updated')]
    });
    window.dispatchEvent(new Event('pagehide'));
    assert.equal(channels.size, 0);
    assert.equal(timers.size, 0);

    window.dispatchEvent(new Event('pageshow'));
    emit('htmx:load');
    assert.equal(channels.size, 1);
    assert.equal(timers.size, 1);
    assert.equal(store.list().length, 1);
});

test('Problem Details suppresses toast headers and presents one failure across HTMX callbacks', t => {
    const { emit, Problem, Toast } = fixture(t);
    const detail = response(409, ' Application/Problem+Json; charset=utf-8 ');
    detail.xhr.responseText = JSON.stringify({ title: 'Category is in use' });
    emit('htmx:beforeOnLoad', detail);
    emit('showToast', { type: 'error', message: 'Duplicate header message' });
    emit('htmx:beforeSwap', detail);
    emit('htmx:responseError', detail);
    emit('htmx:afterRequest', detail);

    assert.equal(detail.shouldSwap, false);
    assert.equal(detail.isError, true);
    assert.equal(Problem.show.mock.callCount(), 1);
    assert.equal(Problem.show.mock.calls[0].arguments[0].title, 'Category is in use');
    assert.equal(Problem.clear.mock.callCount(), 0);
    assert.equal(Toast.show.mock.callCount(), 0);
});

for (const [status, contentType, marker, allowed] of [
    [422, 'text/html; charset=utf-8', 'true', true],
    [422, 'text/html', undefined, false],
    [422, 'text/html', 'True', false],
    [422, 'text/plain', 'true', false],
    [500, 'text/html', 'true', false]
]) {
    test(`validation swap policy: ${status}, ${contentType}, marker ${marker}`, t => {
        const { emit } = fixture(t);
        const detail = response(status, contentType, { 'X-DRN-Validation': marker });
        emit('htmx:beforeSwap', detail);
        assert.equal(detail.shouldSwap, allowed);
        assert.equal(detail.isError, !allowed);
    });
}

for (const [event, title] of [
    ['htmx:sendError', 'Connection lost'],
    ['htmx:timeout', 'The request timed out']
]) {
    test(`${event} gives recovery guidance once`, t => {
        const { emit, Problem } = fixture(t);
        const detail = response(0, '');
        emit(event, detail);
        emit('htmx:responseError', detail);
        assert.equal(Problem.show.mock.callCount(), 1);
        assert.equal(Problem.show.mock.calls[0].arguments[0].title, title);
    });
}

test('intentional request aborts do not show failures', t => {
    const { emit, Problem } = fixture(t);
    emit('htmx:sendAbort', response(0, ''));
    assert.equal(Problem.show.mock.callCount(), 0);
});

for (const contentType of ['application/json', 'text/html; charset=utf-8', 'text/plain']) {
    test(`a non-problem ${contentType} error toast owns feedback once and preserves request privacy`, t => {
        const { emit, Problem, Toast } = fixture(t);
        const element = { closest: selector => selector === '[data-notification-persist]'
            ? { dataset: { notificationPersist: 'false' } }
            : selector === '[data-notification-operation]'
                ? { dataset: { notificationOperation: 'Remove category' } } : null };
        const detail = { ...response(409, contentType), requestConfig: { elt: element } };
        emit('htmx:beforeOnLoad', detail);
        emit('showToast', { type: 'error', message: 'Category is in use', persist: true, duration: 9000 });
        emit('htmx:beforeSwap', detail);
        emit('htmx:responseError', detail);
        emit('showToast', { type: 'error', message: 'Duplicate message' });

        assert.equal(Toast.show.mock.callCount(), 1);
        assert.deepEqual(Toast.show.mock.calls[0].arguments[0], {
            type: 'error', message: 'Category is in use', duration: 9000,
            record: undefined, persist: false, operation: 'Remove category'
        });
        assert.equal(Problem.show.mock.callCount(), 0);
    });
}

for (const message of ['', '   ', null, 42]) {
    test(`an unusable toast message (${JSON.stringify(message)}) keeps the generic failure`, t => {
        const { emit, Problem, Toast } = fixture(t);
        const detail = response(409, 'text/plain');
        emit('htmx:beforeOnLoad', detail);
        emit('showToast', { type: 'error', message });
        emit('htmx:responseError', detail);
        assert.equal(Toast.show.mock.callCount(), 0);
        assert.equal(Problem.show.mock.callCount(), 1);
    });
}

test('toast events outside their response processing are ignored', async t => {
    const { emit, Toast } = fixture(t);
    emit('showToast', { message: 'Uncorrelated feedback' });
    emit('htmx:beforeOnLoad', response());
    await Promise.resolve();
    emit('showToast', { message: 'Delayed feedback' });
    assert.equal(Toast.show.mock.callCount(), 0);
});

test('request context retains the operation path without sensitive query parameters', t => {
    const { emit, Problem } = fixture(t);
    emit('htmx:responseError', { ...response(500), requestConfig: {
        verb: 'post', path: '/app/profile?token=private'
    } });
    assert.equal(Problem.show.mock.calls[0].arguments[1].operation, 'POST /app/profile');
});

test('status import survives stored history and does not resurrect dismissed messages or merge later actions', () => {
    const cache = new Map();
    let sequence = 0;
    const options = {
        storage: () => ({ getItem: key => cache.get(key), setItem: (key, value) => cache.set(key, value),
            removeItem: key => cache.delete(key) }),
        now: () => 100_000_000, newId: () => `status-${++sequence}`
    };
    const context = { app: '/app/', owner: 'account-a' };
    const first = createNotificationStore(options);
    first.configure(context);
    const message = '<strong>Profile updated</strong>';
    const original = statusElement('a'.repeat(32), message);
    importStatusMessages({ querySelectorAll: () => [original] }, first);
    const record = first.list()[0];
    assert.equal(record.message, message);
    assert.equal(record.type, 'success');

    const restored = createNotificationStore(options);
    restored.configure(context);
    const copy = statusElement('a'.repeat(32), message);
    const root = { querySelectorAll: () => [copy] };
    importStatusMessages(root, restored);
    assert.deepEqual(restored.list(), [record]);
    restored.dismiss([record.id]);
    importStatusMessages(root, restored);
    assert.deepEqual(restored.list(), []);

    const later = statusElement('b'.repeat(32), message);
    importStatusMessages({ querySelectorAll: () => [copy, later] }, restored);
    assert.equal(restored.list().length, 1);
    assert.notEqual(restored.list()[0].sourceId, record.sourceId);
    restored.clear();
    importStatusMessages(root, restored);
    assert.deepEqual(restored.list(), []);
});

test('status import respects privacy, valid markup and suspended account state', () => {
    let saved = '';
    const store = createNotificationStore({
        storage: () => ({ getItem: () => null, setItem: (_key, value) => { saved = value; }, removeItem() {} }),
        now: () => 100_000_000, newId: () => 'private-status'
    });
    store.configure({ app: '/app/', owner: 'account-a' });
    const privateStatus = statusElement('c'.repeat(32), 'Private feedback', false);
    const statuses = [statusElement('invalid', 'Invalid marker'), statusElement('d'.repeat(32), '  '), privateStatus];
    const root = { querySelectorAll: () => statuses };
    store.suspend();
    importStatusMessages(root, store);
    assert.deepEqual(store.list(), []);
    assert.equal(privateStatus.dataset.notificationRecorded, undefined);
    store.configure({ app: '/app/', owner: 'account-a' });
    importStatusMessages(root, store);
    assert.equal(store.list().length, 1);
    assert.equal(store.list()[0].persist, false);
    assert.ok(!saved.includes('Private feedback'));
    assert.equal(statuses[0].dataset.notificationRecorded, undefined);
    assert.equal(statuses[1].dataset.notificationRecorded, undefined);
});
