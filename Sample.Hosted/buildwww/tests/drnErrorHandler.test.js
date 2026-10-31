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
    let toastFails = false;
    let beaconAccepted = true;
    let reportOwner = 'account-a';
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
            Toast: { show: value => {
                if (toastFails) throw new Error('Toast renderer failed');
                toasts.push(value);
            } }
        }
    };
    for (const [name, value] of Object.entries({ window,
        document: { querySelector: selector => selector === '[data-notification-context]'
            ? { dataset: { notificationOwner: reportOwner } } : { content: '/app/' } },
        navigator: { userAgent: 'test', sendBeacon: (url, body) => {
            if (transportFails) throw new Error('Offline');
            if (!beaconAccepted) return false;
            reports.push({ url, body });
            return true;
        } },
        fetch: async (url, options) => {
            reports.push({ url, body: new Blob([options.body]) });
        }
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
    assert.equal(toasts.length, 2);
    assert.ok(toasts.every(toast => toast.record === false && !toast.message.includes('Background')));
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
    assert.equal(toasts.length, 2);

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

    // A stale account must not send diagnostics under a potentially different session.
    transportFails = false;
    listeners.error({ message: 'Inactive account failure' });
    assert.equal(reports.length, 3);
    assert.equal(notifications.length, 4);
    active = true;
    listeners.error({ message: 'Over the shared rate limit' });
    assert.equal(reports.length, 3);
    assert.equal(notifications.length, 4);

    timestamp += 60001;
    transportFails = true;
    listeners.unhandledrejection({ reason: 'Offline background failure' });
    assert.equal(notifications.length, 5);
    assert.equal(reports.length, 3);
    assert.equal(toasts.length, 5);

    transportFails = false;
    publishFails = true;
    assert.doesNotThrow(() => listeners.error({ message: 'Broken notification UI' }));
    assert.equal(reports.length, 4);
    publishFails = false;
    duringPublish = () => listeners.error({ message: 'Reentrant error' });
    listeners.error({ message: 'Outer error' });
    assert.equal(reports.length, 5);
    assert.equal(notifications.length, 6);
    duringPublish = null;

    const notificationApi = window.DRN.Notifications;
    delete window.DRN.Notifications;
    assert.doesNotThrow(() => listeners.error({ message: 'Unavailable notification API' }));
    assert.equal(reports.length, 6);
    window.DRN.Notifications = notificationApi;
    assert.doesNotThrow(() => listeners.unhandledrejection({ reason: {
        toString() { throw new Error('Cannot stringify'); }
    } }));
    listeners.error({ message: 'Recovered after malformed rejection' });
    assert.equal(reports.length, 7);
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

    window.DRN.Notifications = notificationApi;
    for (const owner of ['anonymous', '']) {
        reportOwner = owner;
        timestamp += 60001;
        const count = reports.length;
        listeners.error({ message: 'Anonymous browser error' });
        handler.reportActionError(new Error('Anonymous action'), { operation: 'Save' });
        assert.equal(reports.length, count);
    }
    reportOwner = 'account-a';
    timestamp += 60001;
    publishFails = true;
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    publishFails = false;
    toastFails = true;
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    assert.doesNotThrow(() => listeners.error({ message: 'Broken global toast' }));
    toastFails = false;
    delete window.DRN.Notifications;
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    const drn = window.DRN;
    delete window.DRN;
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    window.DRN = drn;
    window.DRN.Notifications = notificationApi;

    // Include UTF-8 expansion and JSON escaping, using both transports and reporting paths.
    for (const accepted of [true, false]) {
        beaconAccepted = accepted;
        for (const character of ['a', '界', '😀', '\u0001']) {
            timestamp += 60001;
            window.location.href = character.repeat(500);
            globalThis.navigator.userAgent = character.repeat(300);
            const error = new Error(character.repeat(500));
            error.stack = character.repeat(2000);
            const reportsBefore = reports.length;
            const record = handler.reportActionError(error, { operation: 'Save' });
            assert.equal(reports.length, reportsBefore + 1);
            const body = reports.at(-1).body;
            assert.ok(body.size <= 3600);
            const payload = JSON.parse(await body.text());
            assert.equal(payload.clientErrorId, record.clientErrorId);
            assert.ok(payload.message.length > 0);
            if (character !== 'a') assert.ok(payload.stack.length < error.stack.length);

            listeners.error({ message: error.message, filename: character.repeat(200), error });
            assert.equal(reports.length, reportsBefore + 2);
            const globalBody = reports.at(-1).body;
            assert.ok(globalBody.size <= 3600);
            const globalPayload = JSON.parse(await globalBody.text());
            assert.equal(globalPayload.clientErrorId, notifications.at(-1).clientErrorId);
        }
    }
});
