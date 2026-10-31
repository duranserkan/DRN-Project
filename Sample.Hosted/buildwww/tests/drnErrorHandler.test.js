import test from 'node:test';
import assert from 'node:assert/strict';
import handler from '../app/js/drn/drnErrorHandler.js';

test('action references correlate reports without exposing diagnostics or notifying for global errors', async t => {
    const reports = [];
    const notifications = [];
    const toasts = [];
    const listeners = {};
    let active = true;
    let transportFails = false;
    const window = {
        location: { href: 'https://sample.test/action' },
        addEventListener: (name, listener) => { listeners[name] = listener; },
        DRN: {
            Notifications: {
                isActive: () => active,
                publish: value => { notifications.push(value); return value; }
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
    assert.equal(notifications.length, 0);
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
    assert.equal(notifications.length, 2);
    active = false;
    assert.equal(handler.reportActionError(new Error('Stale account'), { operation: 'Save' }), null);
    assert.equal(notifications.length, 2);
});
