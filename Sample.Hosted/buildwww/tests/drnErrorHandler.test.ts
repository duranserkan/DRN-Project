import test, { type TestContext } from 'node:test';
import assert from 'node:assert/strict';
import type errorHandler from '../app/ts/drn/drnErrorHandler.ts';
import { installBrowserGlobals } from './unit/browserGlobals.ts';
import { createNotificationStore } from '../app/ts/drn/drnNotifications.ts';

async function fixture(t: TestContext) {
    const reports: { url: string; body: Blob }[] = [];
    type Published = Required<
        Pick<Parameters<IDRN['Notifications']['publish']>[0], 'clientErrorId' | 'message' | 'clientErrorDetails'>
    >;
    const notifications: Published[] = [];
    const toasts: Parameters<IDRN['Toast']['show']>[0][] = [];
    type Listener = (event: Partial<ErrorEvent> & { reason?: unknown }) => void;
    const listeners = {} as Record<'error' | 'unhandledrejection', Listener>;
    const state = {
        active: true,
        transportFails: false,
        publishFails: false,
        toastFails: false,
        beaconAccepted: true,
        reportOwner: 'account-a',
        duringPublish: null as (() => void) | null,
    };
    let timestamp = Date.now();
    t.mock.method(Date, 'now', () => timestamp);
    const window = {
        location: { href: 'https://sample.test/action' },
        addEventListener: (name: keyof typeof listeners, listener: Listener) => {
            listeners[name] = listener;
        },
        DRN: {
            Notifications: {
                isActive: () => state.active,
                publish: (value: Published) => {
                    if (state.publishFails) throw new Error('Notification renderer failed');
                    state.duringPublish?.();
                    notifications.push(value);
                    return value;
                },
            },
            Toast: {
                show: (value: Parameters<IDRN['Toast']['show']>[0]) => {
                    if (state.toastFails) throw new Error('Toast renderer failed');
                    toasts.push(value);
                },
            },
        },
    };
    installBrowserGlobals(t, {
        window,
        document: {
            querySelector: (selector: string) =>
                selector === '[data-notification-context]'
                    ? { dataset: { notificationOwner: state.reportOwner } }
                    : { content: '/app/' },
        },
        navigator: {
            userAgent: 'test',
            sendBeacon: (url: string, body: Blob) => {
                if (state.transportFails) throw new Error('Offline');
                if (!state.beaconAccepted) return false;
                reports.push({ url, body });
                return true;
            },
        },
        fetch: async (url: string, options: { body: string }) => {
            reports.push({ url, body: new Blob([options.body]) });
        },
    });
    // The module owns the report window and deduplication cache for one page lifetime.
    const url = new URL('../app/ts/drn/drnErrorHandler.ts', import.meta.url);
    url.searchParams.set('test', t.name);
    const { default: handler }: { default: typeof errorHandler } = await import(url.href);
    handler.install();
    const nextWindow = () => {
        timestamp += 60001;
    };
    return { handler, state, reports, notifications, toasts, listeners, window, nextWindow };
}

test('action and global errors share safe notification references and suppress duplicates', async (t) => {
    const { handler, reports, notifications, toasts, listeners } = await fixture(t);
    listeners.error({ message: 'Background failure', filename: 'background.js' });
    listeners.unhandledrejection({ reason: new Error('Background rejection') });
    assert.equal(reports.length, 2);
    assert.equal(notifications.length, 2);
    assert.equal(toasts.length, 2);
    assert.ok(toasts.every((toast) => toast.record === false && !toast.message.includes('Background')));
    for (let index = 0; index < 2; index++) {
        const report = JSON.parse(await reports[index]!.body.text());
        assert.match(notifications[index]!.clientErrorId, /^[a-f0-9]{32}$/);
        assert.equal(report.clientErrorId, notifications[index]!.clientErrorId);
        assert.equal(notifications[index]!.message.includes('Background'), false);
        assert.ok(notifications[index]!.clientErrorDetails.includes(report.message));
        assert.ok(notifications[index]!.clientErrorDetails.includes(report.source));
    }
    listeners.error({ message: 'Background failure', filename: 'background.js' });
    listeners.unhandledrejection({ reason: new Error('Background rejection') });
    assert.equal(reports.length, 2);
    assert.equal(notifications.length, 2);
    assert.equal(handler.reportActionError(new Error('Private diagnostic')), null);
    assert.equal(toasts.length, 2);

    const entry = handler.reportActionError(new Error('Private diagnostic'), { operation: 'Save', persist: false });
    assert.ok(entry);
    assert.match(entry.clientErrorId, /^[a-f0-9]{32}$/);
    const report = JSON.parse(await reports[2]!.body.text());
    assert.equal(report.clientErrorId, entry.clientErrorId);
    assert.equal(report.message, 'Private diagnostic');
    assert.equal(reports[2]!.url, '/app/Api/Sample/ClientError/Report');
    assert.equal(entry.message.includes('Private diagnostic'), false);
    assert.ok(entry.clientErrorDetails.includes('Message: Private diagnostic'));
    assert.ok(entry.clientErrorDetails.includes('Stack:'));
    assert.equal(entry.persist, false);
    assert.equal(toasts[0]!.record, false);
});

test('failed transport preserves feedback and inactive accounts suppress reporting', async (t) => {
    const { handler, state, reports, notifications, listeners } = await fixture(t);
    state.transportFails = true;
    const offline = handler.reportActionError(new Error('Offline'), { operation: 'Save' });
    assert.ok(offline);
    assert.equal(notifications.length, 1);
    state.active = false;
    assert.equal(handler.reportActionError(new Error('Stale account'), { operation: 'Save' }), null);
    assert.equal(notifications.length, 1);

    // A stale account must not send diagnostics under a potentially different session.
    state.transportFails = false;
    listeners.error({ message: 'Inactive account failure' });
    assert.equal(reports.length, 0);
    assert.equal(notifications.length, 1);
});

test('actions and global errors share the rate limit and recover in the next window', async (t) => {
    const { handler, reports, notifications, listeners, toasts, state, nextWindow } = await fixture(t);
    for (let i = 0; i < 4; i++) listeners.error({ message: 'Failure ' + i });
    assert.ok(handler.reportActionError(new Error('Fifth failure'), { operation: 'Save' }));
    assert.equal(new Set(notifications.map((entry) => entry.clientErrorId)).size, 5);
    listeners.error({ message: 'Over the shared rate limit' });
    assert.equal(reports.length, 5);
    assert.equal(notifications.length, 5);

    nextWindow();
    state.transportFails = true;
    listeners.unhandledrejection({ reason: 'Offline background failure' });
    assert.equal(notifications.length, 6);
    assert.equal(reports.length, 5);
    assert.equal(toasts.length, 6);
});

test('broken renderers and reentrant global errors do not interrupt subsequent reports', async (t) => {
    const { state, reports, notifications, listeners, window } = await fixture(t);
    state.publishFails = true;
    assert.doesNotThrow(() => listeners.error({ message: 'Broken notification UI' }));
    assert.equal(reports.length, 1);
    state.publishFails = false;
    state.duringPublish = () => listeners.error({ message: 'Reentrant error' });
    listeners.error({ message: 'Outer error' });
    assert.equal(reports.length, 2);
    assert.equal(notifications.length, 1);
    state.duringPublish = null;

    const notificationApi = window.DRN.Notifications;
    Reflect.deleteProperty(window.DRN, 'Notifications');
    assert.doesNotThrow(() => listeners.error({ message: 'Unavailable notification API' }));
    assert.equal(reports.length, 3);
    window.DRN.Notifications = notificationApi;
    assert.doesNotThrow(() =>
        listeners.unhandledrejection({
            reason: {
                toString() {
                    throw new Error('Cannot stringify');
                },
            },
        }),
    );
    listeners.error({ message: 'Recovered after malformed rejection' });
    assert.equal(reports.length, 4);
    assert.equal(notifications.length, 2);
});

test('startup feedback follows the store owner on account binding', async (t) => {
    const { window, reports, listeners, nextWindow } = await fixture(t);
    for (const owner of ['account-a', 'anonymous', '']) {
        nextWindow();
        const store = createNotificationStore({ locks: null, storage: () => null });
        Object.assign(window.DRN, { Notifications: store });
        listeners.error({ message: 'Startup error', filename: 'startup.js' });
        const early = store.list()[0];
        assert.ok(early);
        const payload = JSON.parse(await reports.at(-1)!.body.text());
        assert.equal(early.clientErrorId, payload.clientErrorId);
        store.configure({ app: '/app/', owner });
        assert.deepEqual(store.list(), owner ? [early] : []);
        assert.equal(store.isActive(), !!owner);
    }
});

test('anonymous and missing owners suppress reports', async (t) => {
    const { state, handler, listeners, reports, nextWindow } = await fixture(t);
    for (const owner of ['anonymous', '']) {
        state.reportOwner = owner;
        nextWindow();
        const count: number = reports.length;
        listeners.error({ message: 'Anonymous browser error' });
        handler.reportActionError(new Error('Anonymous action'), { operation: 'Save' });
        assert.equal(reports.length, count);
    }
});

test('actions tolerate missing or failing feedback services', async (t) => {
    const { state, handler, window, listeners } = await fixture(t);
    state.publishFails = true;
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    state.publishFails = false;
    state.toastFails = true;
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    assert.doesNotThrow(() => listeners.error({ message: 'Broken global toast' }));
    state.toastFails = false;
    Reflect.deleteProperty(window.DRN, 'Notifications');
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
    Reflect.deleteProperty(window, 'DRN');
    assert.equal(handler.reportActionError(new Error('Original failure'), { operation: 'Save' }), null);
});

test('UTF-8 and JSON escaping stay within the byte budget on both transports', async (t) => {
    const { state, handler, window, reports, notifications, listeners, nextWindow } = await fixture(t);
    for (const accepted of [true, false]) {
        state.beaconAccepted = accepted;
        for (const character of ['a', '界', '😀', '\u0001']) {
            nextWindow();
            window.location.href = character.repeat(500);
            Object.assign(globalThis.navigator, { userAgent: character.repeat(300) });
            const error = new Error(character.repeat(500));
            error.stack = character.repeat(2000);
            const reportsBefore: number = reports.length;
            const record = handler.reportActionError(error, { operation: 'Save' });
            assert.ok(record);
            assert.equal(reports.length, reportsBefore + 1);
            const body = reports.at(-1)!.body;
            assert.ok(body.size <= 3600);
            const payload = JSON.parse(await body.text());
            assert.equal(payload.clientErrorId, record.clientErrorId);
            assert.ok(payload.message.length > 0);
            assert.equal(typeof payload.diagnosticLimited, 'boolean');
            if (character !== 'a') assert.equal(payload.diagnosticLimited, true);
            if (character !== 'a') assert.ok(payload.stack.length < error.stack.length);

            listeners.error({ message: error.message, filename: character.repeat(200), error });
            assert.equal(reports.length, reportsBefore + 2);
            const globalBody = reports.at(-1)!.body;
            assert.ok(globalBody.size <= 3600);
            const globalPayload = JSON.parse(await globalBody.text());
            assert.equal(globalPayload.clientErrorId, notifications.at(-1)!.clientErrorId);
        }
    }
});

test('action and global diagnostics redact URL secrets and report truncation', async (t) => {
    const { handler, window, reports, notifications, listeners } = await fixture(t);
    window.location.href = 'https://sample.test/action?token=url-probe#access_token=fragment-probe';
    Object.assign(globalThis.navigator, { userAgent: 'test' });
    const sensitive = new Error(
        'Save failed password=message-probe; RecoveryCodes=["first-probe","second-probe"]; retry',
    );
    sensitive.stack = 'Service.Save\n' + 's'.repeat(1980) + '\npassword=stack-probe';
    const safeRecord = handler.reportActionError(sensitive, { operation: 'Save' });
    assert.ok(safeRecord);
    const safeBody = reports.at(-1)!.body;
    const safeJson = await safeBody.text();
    const safePayload = JSON.parse(safeJson);
    assert.ok(safeBody.size <= 3600);
    assert.equal(safePayload.clientErrorId, safeRecord.clientErrorId);
    assert.equal(safePayload.diagnosticRedacted, true);
    assert.equal(safePayload.diagnosticLimited, true);
    assert.equal(safeJson.includes('url-probe'), false);
    assert.equal(safeJson.includes('fragment-probe'), false);
    assert.ok(safeRecord.clientErrorDetails.includes(`Page: ${safePayload.url}`));
    assert.equal(safeRecord.clientErrorDetails.includes('url-probe'), false);
    assert.equal(safeRecord.clientErrorDetails.includes('fragment-probe'), false);
    assert.equal(safeRecord.clientErrorDetails.includes('Some diagnostic values were redacted.'), false);
    assert.ok(safeRecord.clientErrorDetails.includes('Diagnostic details were truncated.'));
    assert.equal(safePayload.message, sensitive.message);
    assert.ok(safePayload.message.endsWith('; retry'));
    assert.ok(safePayload.stack.startsWith('Service.Save'));
    listeners.error({ message: sensitive.message, filename: 'app.js', error: sensitive });
    assert.ok(notifications.at(-1)!.clientErrorDetails.includes(`Page: ${safePayload.url}`));
    assert.equal(notifications.at(-1)!.clientErrorDetails.includes('url-probe'), false);
    assert.equal(notifications.at(-1)!.clientErrorDetails.includes('fragment-probe'), false);
    assert.equal((await reports.at(-1)!.body.text()).includes('url-probe'), false);
});

test('persisted diagnostic history reloads with URL secrets removed', async (t) => {
    const { handler, window, listeners } = await fixture(t);
    window.location.href = 'https://sample.test/action?token=url-probe#access_token=fragment-probe';
    const cache = new Map<string, string>();
    const storage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> = {
        getItem: (key) => cache.get(key) ?? null,
        setItem: (key, value) => cache.set(key, value),
        removeItem: (key) => cache.delete(key),
    };
    const settings = { locks: null, storage: () => storage };
    const store = createNotificationStore(settings);
    store.configure({ app: '/app/', owner: 'account-a' });
    Object.assign(window.DRN, { Notifications: store });
    handler.reportActionError(new Error('Persisted action failure'), { operation: 'Save' });
    listeners.error({ message: 'Persisted global failure', filename: 'app.js' });
    listeners.unhandledrejection({ reason: new Error('Persisted rejection') });
    assert.equal(store.list().length, 3);
    const saved = cache.get('drn.notifications.v1:/app/')!;
    assert.equal(JSON.parse(saved).entries.length, 3);
    assert.equal(saved.includes('url-probe'), false);
    assert.equal(saved.includes('fragment-probe'), false);
    const reloaded = createNotificationStore(settings);
    reloaded.configure({ app: '/app/', owner: 'account-a' });
    assert.deepEqual(reloaded.list(), store.list());
    assert.ok(
        reloaded.list().every((record) => record.clientErrorDetails.includes('Page: https://sample.test/action')),
    );
});

test('oversized action errors keep the local message prefix and limit the server report', async (t) => {
    const { handler, reports } = await fixture(t);
    const prefix = '0123456789'.repeat(50);
    const message = prefix.padEnd(65_537, 'x');
    const record = handler.reportActionError(new Error(message), { operation: 'Save' });
    assert.ok(record);
    assert.equal(record.clientErrorDetails.split('\n', 1)[0], `Message: ${prefix}`);
    assert.ok(record.clientErrorDetails.includes('Diagnostic details were truncated.'));

    assert.equal(reports.length, 1);
    const payload = JSON.parse(await reports[0]!.body.text());
    assert.equal(payload.clientErrorId, record.clientErrorId);
    assert.equal(payload.message, '[diagnostic capture limited]');
    assert.equal(payload.diagnosticLimited, true);
});

test('local action fallback and stack-heading checks use only the displayed prefix', async (t) => {
    const { handler, reports } = await fixture(t);
    const visible = 'x'.repeat(500);
    const blank = ' '.repeat(500);
    const frame = '    at save (app.js:5:23)';
    for (const [message, display] of [
        [visible + 'suffix', visible],
        [blank + 'suffix', 'Action failed'],
        [' '.repeat(65_537), 'Action failed'],
    ] as const) {
        const error = new Error(message);
        error.stack = `Error: ${display}\n${frame}`;
        const record = handler.reportActionError(error, { operation: 'Save' });
        assert.ok(record);
        assert.equal(record.clientErrorDetails.split('\n', 1)[0], `Message: ${display}`);
        assert.ok(record.clientErrorDetails.endsWith(`Stack:\n${frame}`));
    }
    const oversizedReport = JSON.parse(await reports.at(-1)!.body.text());
    assert.equal(oversizedReport.message, '[diagnostic capture limited]');
    assert.equal(oversizedReport.diagnosticLimited, true);
});

test('action messages normalize unusual thrown values', async (t) => {
    const { handler, reports, nextWindow } = await fixture(t);
    for (const [error, expected] of [
        [0, '0'],
        [false, 'false'],
        [42n, '42'],
        [Symbol('failure'), 'Symbol(failure)'],
        ['', 'Action failed'],
        [' \t\n', 'Action failed'],
        ['\u0085', 'Action failed'],
        ['  Save failed  ', '  Save failed  '],
        [' '.repeat(500) + 'x', 'Action failed'],
        [' '.repeat(499) + 'xy', ' '.repeat(499) + 'x'],
        [new Error(), 'Action failed'],
        [new Error('  '), 'Action failed'],
        [{ toString: () => 'private object details' }, 'Action failed'],
        [() => 'private function details', 'Action failed'],
        [null, 'Action failed'],
        [undefined, 'Action failed'],
    ] as const) {
        nextWindow();
        const record = handler.reportActionError(error, { operation: 'Save' });
        assert.ok(record);
        const payload = JSON.parse(await reports.at(-1)!.body.text());
        assert.equal(payload.clientErrorId, record.clientErrorId);
        assert.equal(payload.message, expected);
        assert.equal(record.clientErrorDetails.split('\n', 1)[0], `Message: ${expected}`);
    }
});

test('action and global handlers capture and format stack frames once', async (t) => {
    const { handler, listeners, reports, notifications, nextWindow } = await fixture(t);
    for (const mode of ['action', 'error', 'unhandledrejection']) {
        for (const [stack, hasFrames, displayedStack = stack] of [
            ['', false],
            ['Error: Stack capture probe', false, ''],
            [
                'Error: Stack capture probe\n    at HTMLButtonElement.onClick (<anonymous>:5:23)',
                true,
                '    at HTMLButtonElement.onClick (<anonymous>:5:23)',
            ],
            [
                'TypeError: Stack capture probe\r\n    at save (app.js:5:23)',
                true,
                mode === 'error'
                    ? 'TypeError: Stack capture probe\r\n    at save (app.js:5:23)'
                    : '    at save (app.js:5:23)',
            ],
            ['Error: Different diagnostic\n    at save (app.js:5:23)', true],
            ['onClick@https://sample.test/app.js:5:23', true],
            ['@https://sample.test/app.js:5:23', true],
            ['onClick@https://user@sample.test/app.js:5:23', true],
            ['@a:5', true],
            ['@:5', false, '@:5\nStack frames unavailable'],
            ['@'.repeat(1500), false, '@'.repeat(1500) + '\nStack frames unavailable'],
            [
                'Error:\t  Stack capture probe\n    at save (app.js:5:23)',
                true,
                mode === 'error'
                    ? 'Error:\t  Stack capture probe\n    at save (app.js:5:23)'
                    : '    at save (app.js:5:23)',
            ],
        ] as const) {
            nextWindow();
            const error = new Error('Stack capture probe');
            let reads = 0;
            Object.defineProperty(error, 'stack', {
                get() {
                    if (++reads > 1) throw new Error('Stack must be captured once');
                    return stack;
                },
            });
            const before: number = reports.length;
            const notificationsBefore: number = notifications.length;
            if (mode === 'action') handler.reportActionError(error, { operation: 'Save' });
            else if (mode === 'error')
                listeners.error({
                    message: 'Uncaught Error: Stack capture probe',
                    filename: 'app.js',
                    lineno: 5,
                    colno: 17,
                    error,
                });
            else listeners.unhandledrejection({ reason: error });

            assert.equal(reads, 1, mode);
            assert.equal(reports.length, before + 1, mode);
            assert.equal(notifications.length, notificationsBefore + 1, mode);
            const payload = JSON.parse(await reports.at(-1)!.body.text());
            const details = notifications.at(-1)!.clientErrorDetails;
            assert.equal(payload.stack, stack, mode);
            if (displayedStack) {
                assert.ok(details.endsWith(`Stack:\n${displayedStack}`), mode);
            } else assert.ok(details.endsWith('Stack:\nStack frames unavailable'), mode);
            assert.ok(details.indexOf('Page:') < details.indexOf('Stack:\n'), mode);
            assert.equal(details.includes('Stack frames unavailable'), !hasFrames, mode);
            if (mode === 'error') {
                assert.ok(details.includes('Source: app.js'));
                assert.ok(details.includes('Line: 5, column: 17'));
            }
        }
    }
});
