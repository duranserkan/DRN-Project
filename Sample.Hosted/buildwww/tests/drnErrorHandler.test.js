import test from 'node:test';
import assert from 'node:assert/strict';
import handler from '../app/js/drn/drnErrorHandler.js';
import { createNotificationStore } from '../app/js/drn/drnNotifications.js';

test('action and global errors share safe notification references with bounded, resilient reporting', async t => {
    const reports = [];
    const notifications = [];
    const toasts = [];
    const listeners = {};
    let active = true;
    let transportFails = false;
    let publishFails = false;
    let duringPublish = null;
    let timestamp = Date.now();
    t.mock.method(Date, 'now', () => timestamp);
    const window = {
        location: { href: 'https://sample.test/action' },
        addEventListener: (name, listener) => { listeners[name] = listener; },
        DRN: {
            Notifications: {
                isActive: () => active,
                publish: value => {
                    if (publishFails) throw new Error('Notification renderer failed');
                    duringPublish?.();
                    notifications.push(value);
                    return value;
                }
            },
            Toast: { show: value => toasts.push(value) }
        }
    };
    for (const [name, value] of Object.entries({ window,
        document: { querySelector: () => ({ content: '/app/' }) },
        navigator: { userAgent: 'test', sendBeacon: (url, body) => {
            if (transportFails) throw new Error('Offline');
            reports.push({ url, body });
            return true;
        } }
    })) {
        const original = Object.getOwnPropertyDescriptor(globalThis, name);
        Object.defineProperty(globalThis, name, { configurable: true, value });
        t.after(() => {
            if (original) Object.defineProperty(globalThis, name, original);
            else delete globalThis[name];
        });
    }

    handler.install();
    listeners.error({ message: 'Background failure', filename: 'background.js' });
    listeners.unhandledrejection({ reason: new Error('Background rejection') });
    assert.equal(reports.length, 2);
    assert.equal(notifications.length, 2);
    assert.equal(toasts.length, 0);
    for (let index = 0; index < 2; index++) {
        const report = JSON.parse(await reports[index].body.text());
        assert.match(notifications[index].clientErrorId, /^[a-f0-9]{32}$/);
        assert.equal(report.clientErrorId, notifications[index].clientErrorId);
        assert.equal(JSON.stringify(notifications[index]).includes('Background'), false);
        assert.equal(JSON.stringify(notifications[index]).includes('background.js'), false);
    }
    listeners.error({ message: 'Background failure', filename: 'background.js' });
    listeners.unhandledrejection({ reason: new Error('Background rejection') });
    assert.equal(reports.length, 2);
    assert.equal(notifications.length, 2);
    assert.equal(handler.reportActionError(new Error('Private diagnostic')), null);

    const entry = handler.reportActionError(new Error('Private diagnostic'), { operation: 'Save', persist: false });
    assert.match(entry.clientErrorId, /^[a-f0-9]{32}$/);
    const report = JSON.parse(await reports[2].body.text());
    assert.equal(report.clientErrorId, entry.clientErrorId);
    assert.equal(report.message, 'Private diagnostic');
    assert.equal(reports[2].url, '/app/Api/Sample/ClientError/Report');
    assert.equal(JSON.stringify(entry).includes('Private diagnostic'), false);
    assert.equal(entry.persist, false);
    assert.equal(toasts[0].record, false);

    transportFails = true;
    const offline = handler.reportActionError(new Error('Offline'), { operation: 'Save' });
    assert.notEqual(offline.clientErrorId, entry.clientErrorId);
    assert.equal(notifications.length, 4);
    active = false;
    assert.equal(handler.reportActionError(new Error('Stale account'), { operation: 'Save' }), null);
    assert.equal(notifications.length, 4);

    // A suspended account still permits telemetry, but must not retain notifications.
    transportFails = false;
    listeners.error({ message: 'Inactive account failure' });
    assert.equal(reports.length, 4);
    assert.equal(notifications.length, 4);
    active = true;
    listeners.error({ message: 'Over the shared rate limit' });
    assert.equal(reports.length, 4);
    assert.equal(notifications.length, 4);

    timestamp += 60001;
    transportFails = true;
    listeners.unhandledrejection({ reason: 'Offline background failure' });
    assert.equal(notifications.length, 5);
    assert.equal(reports.length, 4);
    assert.equal(toasts.length, 2);

    transportFails = false;
    publishFails = true;
    assert.doesNotThrow(() => listeners.error({ message: 'Broken notification UI' }));
    assert.equal(reports.length, 5);
    publishFails = false;
    duringPublish = () => listeners.error({ message: 'Reentrant error' });
    listeners.error({ message: 'Outer error' });
    assert.equal(reports.length, 6);
    assert.equal(notifications.length, 6);
    duringPublish = null;

    const notificationApi = window.DRN.Notifications;
    delete window.DRN.Notifications;
    assert.doesNotThrow(() => listeners.error({ message: 'Unavailable notification API' }));
    assert.equal(reports.length, 7);
    window.DRN.Notifications = notificationApi;
    assert.doesNotThrow(() => listeners.unhandledrejection({ reason: {
        toString() { throw new Error('Cannot stringify'); }
    } }));
    listeners.error({ message: 'Recovered after malformed rejection' });
    assert.equal(reports.length, 8);
    assert.equal(notifications.length, 7);

    // Exercise the real store lifecycle rather than only a publish stub.
    for (const owner of ['account-a', 'anonymous', '']) {
        timestamp += 60001;
        const store = createNotificationStore({ locks: null, storage: () => null });
        window.DRN.Notifications = store;
        listeners.error({ message: 'Startup error', filename: 'startup.js' });
        const early = store.list()[0];
        assert.ok(early);
        const payload = JSON.parse(await reports.at(-1).body.text());
        assert.equal(early.clientErrorId, payload.clientErrorId);
        store.configure({ app: '/app/', owner });
        assert.deepEqual(store.list(), owner ? [early] : []);
        assert.equal(store.isActive(), !!owner);
    }
});
