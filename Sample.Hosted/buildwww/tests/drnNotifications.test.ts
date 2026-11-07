import test from 'node:test';
import assert from 'node:assert/strict';
import { createNotificationStore } from '../app/ts/drn/drnNotifications.ts';
import { filterNotifications, groupNotifications } from '../app/ts/drn/drnNotificationPanel.ts';

type StoreOptions = NonNullable<Parameters<typeof createNotificationStore>[0]>;

function fixture(
    options: Omit<StoreOptions, 'locks'> & {
        locks?: { request(name: string, callback: () => void): Promise<void> } | null;
    } = {},
) {
    const cache = new Map<string, string>();
    let timestamp = 100_000_000;
    let sequence = 0;
    const storage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> = {
        getItem: (key) => cache.get(key) ?? null,
        setItem: (key, value) => cache.set(key, value),
        removeItem: (key) => cache.delete(key),
    };
    const settings = {
        storage: () => storage,
        now: () => timestamp,
        newId: () => `id-${++sequence}`,
        ...options,
        locks: (options.locks as LockManager | null) ?? null,
    };
    const store = createNotificationStore(settings);
    store.configure({ app: '/app/', owner: 'account-a' });
    return {
        store,
        cache,
        settings,
        storage,
        advance: (duration: number) => {
            timestamp += duration;
        },
    };
}

test('a queued publication recovers if the saved JSON is corrupted before the lock is granted', async () => {
    const locks = queuedLocks();
    const { store, cache, settings } = fixture({ locks });
    await locks.drain();
    const entry = store.publish({ message: 'Keep this notification' });
    assert.ok(entry);
    cache.set('drn.notifications.v1:/app/', '{');
    await locks.drain();
    const restored = createNotificationStore(settings);
    restored.configure({ app: '/app/', owner: 'account-a' });
    assert.deepEqual(restored.list(), [entry]);
    await locks.drain();
});

for (const useLocks of [false, true]) {
    test(`initial configuration retains startup feedback alongside saved history, locks: ${useLocks}`, async () => {
        const locks = useLocks ? queuedLocks() : null;
        const { store, settings, advance } = fixture({ locks });
        const saved = store.publish({ message: 'Saved history' });
        assert.ok(saved);
        await locks?.drain();
        advance(1);
        const starting = createNotificationStore(settings);
        const early = starting.publish({ type: 'error', message: 'Startup failed', clientErrorId: 'a'.repeat(32) });
        assert.ok(early);
        const local = starting.publish({ message: 'Local startup feedback', persist: false });
        assert.ok(local);
        starting.configure({ app: '/app/', owner: 'account-a' });
        assert.deepEqual(new Set(starting.list().map((entry) => entry.id)), new Set([saved.id, early.id, local.id]));
        await locks?.drain();
        assert.deepEqual(
            starting.list().find((entry) => entry.id === early.id),
            early,
        );
        assert.deepEqual(
            starting.list().find((entry) => entry.id === local.id),
            local,
        );
        const reloaded = createNotificationStore(settings);
        reloaded.configure({ app: '/app/', owner: 'account-a' });
        await locks?.drain();
        assert.deepEqual(reloaded.list(), [early, saved]);

        starting.configure({ app: '/app/', owner: 'account-b' });
        await locks?.drain();
        assert.deepEqual(starting.list(), []);
    });

    for (const invalidate of ['missing owner', 'suspended']) {
        test(`startup feedback is discarded for ${invalidate}, locks: ${useLocks}`, async () => {
            const locks = useLocks ? queuedLocks() : null;
            const { settings } = fixture({ locks });
            await locks?.drain();
            const starting = createNotificationStore(settings);
            starting.publish({ message: 'Do not retain', clientErrorId: 'b'.repeat(32) });
            if (invalidate === 'suspended') starting.suspend();
            starting.configure({ app: '/app/', owner: invalidate === 'missing owner' ? '' : 'account-a' });
            await locks?.drain();
            assert.deepEqual(starting.list(), []);
            starting.configure({ app: '/app/', owner: 'account-b' });
            await locks?.drain();
            assert.deepEqual(starting.list(), []);
        });
    }
}

test('client error references survive storage and are searchable without using requestId', () => {
    const { store, settings } = fixture();
    const clientErrorId = 'a'.repeat(32);
    const clientErrorDetails = 'Message: Browser failure\nStack:\nsaveProfile';
    const entry = store.publish({ type: 'error', message: 'Save failed', clientErrorId, clientErrorDetails });
    assert.ok(entry);
    const restored = createNotificationStore(settings);
    restored.configure({ app: '/app/', owner: 'account-a' });
    assert.deepEqual(restored.list(), [entry]);
    assert.equal(entry.requestId, '');
    assert.deepEqual(filterNotifications(restored.list(), 'error', clientErrorId), [entry]);
    assert.equal(entry.clientErrorDetails, clientErrorDetails);
    assert.deepEqual(filterNotifications(restored.list(), 'error', 'saveProfile'), [entry]);
    const backend = store.publish({ message: 'Backend failure', requestId: 'request-a', clientErrorDetails });
    assert.ok(backend);
    assert.equal(backend.clientErrorDetails, '');
    const bounded = store.publish({ message: 'Long failure', clientErrorId, clientErrorDetails: 'x'.repeat(5000) });
    assert.ok(bounded);
    assert.equal(bounded.clientErrorDetails.length, 4000);
    store.configure({ app: '/app/', owner: 'account-b' });
    assert.deepEqual(store.list(), []);
});

test('default IDs preserve publishing, persistence and individual dismissal without randomUUID', () => {
    const originalCrypto = Object.getOwnPropertyDescriptor(globalThis, 'crypto');
    let sequence = 0;
    Object.defineProperty(globalThis, 'crypto', {
        configurable: true,
        value: {
            getRandomValues(bytes: Uint8Array) {
                bytes.fill(++sequence);
                return bytes;
            },
        },
    });
    try {
        const { store, settings, advance } = fixture({ newId: undefined });
        const first = store.publish({ type: 'error', message: 'Save failed' });
        assert.ok(first);
        advance(1);
        const second = store.publish({ type: 'success', message: 'Save completed' });
        assert.ok(second);
        assert.equal(typeof first.id, 'string');
        assert.ok(first.id.length > 0 && first.id.length <= 100);
        assert.notEqual(first.id, second.id);
        assert.deepEqual(store.list(), [second, first]);

        const restored = createNotificationStore(settings);
        restored.configure({ app: '/app/', owner: 'account-a' });
        assert.deepEqual(restored.list(), [second, first]);
        assert.deepEqual(restored.dismiss([first.id]), [first]);
        assert.deepEqual(restored.list(), [second]);
    } finally {
        if (originalCrypto) Object.defineProperty(globalThis, 'crypto', originalCrypto);
        else Reflect.deleteProperty(globalThis, 'crypto');
    }
});

// Hold every exclusive callback until both simulated tabs have made their local mutations.
function queuedLocks() {
    const requests: { name: string; callback(): void; resolve(): void; reject(error: unknown): void }[] = [];
    return {
        request(name: string, callback: () => void) {
            return new Promise<void>((resolve, reject) => requests.push({ name, callback, resolve, reject }));
        },
        async drain() {
            // Let each store submit its coalesced work before draining the shared lock queue.
            await Promise.resolve();
            while (requests.length) {
                const request = requests.shift()!;
                assert.equal(request.name, 'drn.notifications.v1:/app/');
                try {
                    request.callback();
                    request.resolve();
                } catch (error) {
                    request.reject(error);
                }
                // Allow the store's completion handlers to reconcile its local view.
                await new Promise((resolve) => setImmediate(resolve));
            }
        },
    };
}

test('synchronous publications, acknowledgment and undo share one ordered storage write', async () => {
    const locks = queuedLocks();
    const { store, storage, cache, advance } = fixture({ locks });
    await locks.drain();
    const writes: string[] = [];
    storage.setItem = (key, value) => {
        writes.push(value);
        cache.set(key, value);
    };
    const first = store.publish({ message: 'First occurrence' });
    assert.ok(first);
    advance(1);
    const second = store.publish({ message: 'Second occurrence', sourceId: 'second' });
    assert.ok(second);
    store.markSeen();
    store.restore(store.dismiss([second.id]));
    assert.deepEqual(store.publish({ message: 'Duplicate occurrence', sourceId: 'second' }), { ...second, seen: true });
    advance(1);
    const local = store.publish({ message: 'Only this page', persist: false });
    assert.ok(local);
    assert.equal(writes.length, 0);
    await locks.drain();
    const expected = [
        { ...second, seen: true },
        { ...first, seen: true },
    ];
    assert.equal(writes.length, 1);
    assert.deepEqual(JSON.parse(writes[0]!).entries, expected);
    assert.deepEqual(store.list(), [local, ...expected]);

    store.expire();
    await locks.drain();
    assert.equal(writes.length, 1);
});

test('later publications keep their place behind another tab in the lock queue', async () => {
    const locks = queuedLocks();
    const { store, settings } = fixture({ locks });
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-a' });
    await locks.drain();
    store.publish({ message: 'First queued change' });
    await Promise.resolve();
    const firstSource = other.publish({ message: 'Other tab owns this source', sourceId: 'shared-source' });
    assert.ok(firstSource);
    await Promise.resolve();
    store.publish({ message: 'Later duplicate', sourceId: 'shared-source' });
    await locks.drain();
    store.sync();
    assert.deepEqual(
        store.list().find((entry) => entry.sourceId === 'shared-source'),
        firstSource,
    );
});

for (const maxEntries of [1000, 3]) {
    test(`same-timestamp publications preserve order through locked persistence, capacity: ${maxEntries}`, async () => {
        const locks = queuedLocks();
        const { store, settings } = fixture({ locks, maxEntries });
        await locks.drain();
        // The fixture clock stays fixed so equal timestamps exercise stable-sort ordering.
        const published = ['First', 'Second', 'Third', 'Fourth'].map((message) => store.publish({ message }));
        const expected = published.toReversed().slice(0, maxEntries);
        assert.deepEqual(store.list(), expected);
        await locks.drain();
        assert.deepEqual(store.list(), expected);
        store.sync();
        assert.deepEqual(store.list(), expected);

        const restored = createNotificationStore(settings);
        restored.configure({ app: '/app/', owner: 'account-a' });
        assert.deepEqual(restored.list(), expected);
        await locks.drain();
        assert.deepEqual(restored.list(), expected);
    });
}

test('contending publications and initialization merge the latest locked snapshot', async () => {
    const locks = queuedLocks();
    const { store, settings } = fixture({ locks });
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-a' });
    const first = store.publish({ message: 'First tab' });
    assert.ok(first);
    const second = other.publish({ message: 'Second tab' });
    assert.ok(second);
    // A newly opened tab has an empty snapshot while both publications wait for the lock.
    const arriving = createNotificationStore(settings);
    arriving.configure({ app: '/app/', owner: 'account-a' });
    store.sync();
    assert.deepEqual(store.list(), [first]);
    await locks.drain();
    for (const tab of [store, other, arriving]) {
        tab.sync();
        assert.deepEqual(new Set(tab.list().map((entry) => entry.id)), new Set([first.id, second.id]));
    }
});

for (const dismissFirst of [true, false]) {
    test(`concurrent dismissal and publication preserve both changes, dismiss first: ${dismissFirst}`, async () => {
        const locks = queuedLocks();
        const { store, settings } = fixture({ locks });
        const old = store.publish({ message: 'Dismiss me' });
        assert.ok(old);
        await locks.drain();
        const other = createNotificationStore(settings);
        other.configure({ app: '/app/', owner: 'account-a' });
        await locks.drain();
        let next;
        if (dismissFirst) {
            store.dismiss([old.id]);
            next = other.publish({ message: 'Keep me' });
        } else {
            next = other.publish({ message: 'Keep me' });
            store.dismiss([old.id]);
        }
        await locks.drain();
        store.sync();
        other.sync();
        assert.deepEqual(store.list(), [next]);
        assert.deepEqual(other.list(), [next]);
    });
}

test('queued seen changes do not acknowledge concurrent arrivals or share memory-only entries', async () => {
    const locks = queuedLocks();
    const { store, settings } = fixture({ locks });
    const old = store.publish({ message: 'Existing' });
    assert.ok(old);
    await locks.drain();
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-a' });
    await locks.drain();
    const next = other.publish({ message: 'Arriving' });
    assert.ok(next);
    const local = store.publish({ message: 'Only here', persist: false });
    assert.ok(local);
    store.markSeen();
    await locks.drain();
    store.sync();
    other.sync();
    assert.deepEqual(
        other.list().find((entry) => entry.id === old.id),
        { ...old, seen: true },
    );
    assert.deepEqual(
        other.list().find((entry) => entry.id === next.id),
        next,
    );
    assert.ok(!other.list().some((entry) => entry.id === local.id));
    assert.deepEqual(
        store.list().find((entry) => entry.id === local.id),
        { ...local, seen: true },
    );
});

test('an owner change rejects old queued writes and preserves the new account history', async () => {
    const locks = queuedLocks();
    const { store, settings } = fixture({ locks });
    await locks.drain();
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-b' });
    const current = other.publish({ message: 'Account B' });
    assert.ok(current);
    store.publish({ message: 'Stale account A' });
    await locks.drain();
    other.sync();
    assert.equal(store.isActive(), false);
    assert.deepEqual(store.list(), []);
    assert.deepEqual(other.list(), [current]);
});

test('concurrent duplicate sources retain one occurrence and its acknowledgment', async () => {
    const locks = queuedLocks();
    const { store, settings } = fixture({ locks });
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-a' });
    const first = store.publish({ message: 'Imported status', sourceId: 'status-one' });
    assert.ok(first);
    other.publish({ message: 'Imported status', sourceId: 'status-one' });
    other.markSeen();
    await locks.drain();
    store.sync();
    other.sync();
    assert.deepEqual(store.list(), [{ ...first, seen: true }]);
    assert.deepEqual(other.list(), [{ ...first, seen: true }]);
});

test('queued dismissal removes a duplicate source with another shared ID and supports undo', async () => {
    const locks = queuedLocks();
    const { store, settings } = fixture({ locks });
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-a' });
    await locks.drain();
    const first = store.publish({ message: 'Imported status', sourceId: 'status-one' });
    assert.ok(first);
    const duplicate = other.publish({ message: 'Imported status', sourceId: 'status-one' });
    assert.ok(duplicate);
    assert.notEqual(first.id, duplicate.id);
    other.markSeen();
    const removed = other.dismiss([duplicate.id]);
    const unrelated = store.publish({ message: 'Keep this', sourceId: 'status-two' });
    assert.ok(unrelated);
    await locks.drain();
    for (const tab of [store, other]) {
        tab.sync();
        assert.deepEqual(tab.list(), [unrelated]);
    }

    other.restore(removed);
    await locks.drain();
    store.sync();
    assert.equal(store.list().filter((entry) => entry.sourceId === 'status-one').length, 1);
    assert.deepEqual(
        store.list().find((entry) => entry.sourceId === 'status-one'),
        { ...duplicate, seen: true },
    );
});

test('failed lock requests keep local feedback without writing shared storage', async () => {
    const locks = { request: () => Promise.reject(new Error('Locks unavailable')) };
    const { store, cache } = fixture({ locks });
    const entry = store.publish({ message: 'Local feedback' });
    assert.ok(entry);
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(cache.size, 0);
    store.sync();
    assert.deepEqual(store.list(), [entry]);
});

test('synchronously throwing lock requests keep local feedback without writing shared storage', async () => {
    const locks = {
        request() {
            throw new Error('Locks unavailable');
        },
    };
    const { store, cache } = fixture({ locks });
    const entry = store.publish({ message: 'Local feedback' });
    assert.ok(entry);
    await new Promise((resolve) => setImmediate(resolve));
    assert.equal(cache.size, 0);
    store.sync();
    assert.deepEqual(store.list(), [entry]);
});

test('suspension cancels queued initialization and publications', async () => {
    const locks = queuedLocks();
    const { store, cache } = fixture({ locks });
    store.publish({ message: 'Do not persist' });
    store.suspend();
    await locks.drain();
    assert.equal(cache.size, 0);
    assert.deepEqual(store.list(), []);
});

test('a local account switch cancels its previous queued writes', async () => {
    const locks = queuedLocks();
    const { store, cache } = fixture({ locks });
    store.publish({ message: 'Account A pending' });
    store.configure({ app: '/app/', owner: 'account-b' });
    const entry = store.publish({ message: 'Account B pending' });
    assert.ok(entry);
    await locks.drain();
    assert.deepEqual(store.list(), [entry]);
    assert.deepEqual(JSON.parse([...cache.values()][0]!).entries, [entry]);
});

test('a failed shared write preserves both saved history and local feedback', async () => {
    const locks = queuedLocks();
    const { store, storage, cache } = fixture({ locks });
    const saved = store.publish({ message: 'Already saved' });
    assert.ok(saved);
    await locks.drain();
    storage.setItem = () => {
        throw new Error('Quota exceeded');
    };
    const local = store.publish({ message: 'Not saved' });
    assert.ok(local);
    await locks.drain();
    store.sync();
    assert.deepEqual(new Set(store.list().map((entry) => entry.id)), new Set([saved.id, local.id]));
    assert.deepEqual(JSON.parse([...cache.values()][0]!).entries, [saved]);
});

test('without Web Locks the default store uses tab-local storage only', () => {
    const originalSession = Object.getOwnPropertyDescriptor(globalThis, 'sessionStorage');
    const originalLocal = Object.getOwnPropertyDescriptor(globalThis, 'localStorage');
    const { storage, cache, settings } = fixture();
    Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, value: storage });
    Object.defineProperty(globalThis, 'localStorage', {
        configurable: true,
        get() {
            return assert.fail('Unlocked shared storage must not be used');
        },
    });
    try {
        const store = createNotificationStore({ now: settings.now, newId: settings.newId, locks: null });
        store.configure({ app: '/app/', owner: 'account-a' });
        const entry = store.publish({ message: 'Tab-local fallback' });
        assert.ok(entry);
        assert.deepEqual(JSON.parse([...cache.values()][0]!).entries, [entry]);
    } finally {
        if (originalSession) Object.defineProperty(globalThis, 'sessionStorage', originalSession);
        else Reflect.deleteProperty(globalThis, 'sessionStorage');
        if (originalLocal) Object.defineProperty(globalThis, 'localStorage', originalLocal);
        else Reflect.deleteProperty(globalThis, 'localStorage');
    }
});

test('default IDs retain native randomUUID when it is available', () => {
    const originalCrypto = Object.getOwnPropertyDescriptor(globalThis, 'crypto');
    const id = '8cdb9d4f-7a85-4399-988f-15ae79bc3ed8';
    Object.defineProperty(globalThis, 'crypto', {
        configurable: true,
        value: {
            randomUUID() {
                assert.equal(this, globalThis.crypto);
                return id;
            },
            getRandomValues() {
                assert.fail('The native UUID path must not use the fallback.');
            },
        },
    });
    try {
        const { store } = fixture({ newId: undefined });
        const entry = store.publish({ message: 'Ready' });
        assert.ok(entry);
        assert.equal(entry.id, id);
        assert.deepEqual(store.list(), [entry]);
    } finally {
        if (originalCrypto) Object.defineProperty(globalThis, 'crypto', originalCrypto);
        else Reflect.deleteProperty(globalThis, 'crypto');
    }
});

test('reload and undo preserve seen history, exclude memory-only entries and isolate account/application boundaries', () => {
    const { store, settings, cache } = fixture();
    const first = store.publish({ message: 'Ready', sourceId: 'operation-one' });
    assert.ok(first);
    assert.equal(first.seen, false);
    store.markSeen();
    assert.deepEqual(store.list(), [{ ...first, seen: true }]);
    assert.equal(store.publish({ message: 'Ready', sourceId: 'operation-one' })!.seen, true);
    store.publish({ type: 'warning', message: 'Memory-only detail', persist: false });
    assert.equal(store.list().length, 2);
    assert.ok(![...cache.values()][0]!.includes('Memory-only detail'));

    const restored = createNotificationStore(settings);
    restored.configure({ app: '/app/', owner: 'account-a' });
    assert.deepEqual(restored.list(), [{ ...first, seen: true }]);
    const removed = restored.clear();
    restored.restore(removed);
    assert.equal(restored.list()[0]!.seen, true);
    // @ts-expect-error A caller cannot set the seen flag when publishing.
    const next = restored.publish({ message: 'Another operation', seen: true });
    assert.ok(next);
    assert.equal(next.seen, false);
    assert.deepEqual(
        restored.list().filter((entry) => !entry.seen),
        [next],
    );

    restored.configure({ app: '/app/', owner: 'account-b' });
    restored.markSeen();
    assert.deepEqual(restored.list(), []);
    restored.publish({ type: 'info', message: 'Account B' });
    restored.configure({ app: '/other/', owner: 'account-b' });
    assert.deepEqual(restored.list(), []);
    restored.suspend();
    restored.markSeen();
    assert.equal(restored.isActive(), false);
});

test('tabs share history and seen state without reviving dismissed entries or sharing memory-only content', () => {
    const { store, settings } = fixture();
    const other = createNotificationStore(settings);
    other.configure({ app: '/app/', owner: 'account-a' });
    const first = store.publish({ message: 'Report ready' });
    assert.ok(first);
    const privateEntry = store.publish({ message: 'Current page only', persist: false });
    assert.ok(privateEntry);
    other.sync();
    assert.deepEqual(other.list(), [first]);
    other.markSeen();
    store.sync();
    assert.equal(store.list().find((entry) => entry.id === first.id)!.seen, true);
    assert.equal(store.list().find((entry) => entry.id === privateEntry.id)!.seen, false);

    other.dismiss([first.id]);
    // Publishing before a storage event arrives still reads the other tab's latest state.
    const next = store.publish({ message: 'Another report' });
    assert.ok(next);
    other.expire();
    assert.deepEqual(other.list(), [next]);
    other.clear();
    store.sync();
    assert.deepEqual(store.list(), [privateEntry]);

    other.configure({ app: '/app/', owner: 'account-b' });
    const accountB = other.publish({ message: 'Account B report' });
    assert.ok(accountB);
    assert.equal(store.publish({ message: 'Stale account A response' }), null);
    assert.equal(store.isActive(), false);
    store.suspend();
    other.sync();
    assert.deepEqual(other.list(), [accountB]);
});

test('count, byte and age ceilings apply before presentation grouping', () => {
    const { store, advance } = fixture();
    for (let index = 0; index < 1005; index++) {
        advance(1);
        store.publish({ type: 'error', message: 'Repeated failure' });
    }
    assert.equal(store.list().length, 1000);
    assert.equal(store.list().at(-1)!.id, 'id-6');
    advance(24 * 60 * 60 * 1000);
    store.expire();
    assert.deepEqual(store.list(), []);

    const bounded = fixture({ maxBytes: 1500 });
    for (let index = 0; index < 10; index++) {
        bounded.advance(1);
        bounded.store.publish({ type: 'warning', message: '💡'.repeat(100) });
    }
    const saved = [...bounded.cache.values()][0];
    assert.ok(new TextEncoder().encode(saved).length <= 1500);
    assert.ok(bounded.store.list().length < 10);
    assert.equal(bounded.store.list()[0]!.id, 'id-10');
});

test('list reads expire entries at their individual age boundaries', () => {
    const { store, advance } = fixture({ maxAge: 10 });
    const first = store.publish({ message: 'First' });
    assert.ok(first);
    advance(3);
    const second = store.publish({ message: 'Second' });
    assert.ok(second);
    advance(6);
    assert.deepEqual(store.list(), [second, first]);
    advance(1);
    assert.deepEqual(store.list(), [second]);
    advance(3);
    assert.deepEqual(store.list(), []);
});

test('corrupt, oversized, duplicate, future and unsafe restored fields cannot enter history', () => {
    const { settings, storage, store } = fixture();
    const entry = store.publish({ type: 'error', message: '<script>text only</script>' });
    assert.ok(entry);
    const key = 'drn.notifications.v1:/app/';
    const restore = (value: string) => {
        storage.setItem(key, value);
        const next = createNotificationStore(settings);
        next.configure({ app: '/app/', owner: 'account-a' });
        return next.list();
    };
    assert.deepEqual(restore('{'), []);
    assert.deepEqual(restore('x'.repeat(1024 * 1024 + 1)), []);
    const entries = [
        entry,
        entry,
        { ...entry, id: 'future', createdAt: entry.createdAt + 1 },
        { ...entry, id: 'bad-type', type: '__proto__' },
        {
            ...entry,
            id: 'fields',
            requestId: 'r'.repeat(201),
            sourceId: 's'.repeat(201),
            headers: { authorization: 'secret' },
        },
    ];
    const actual = restore(JSON.stringify({ version: 1, owner: 'account-a', entries }));
    assert.equal(actual.length, 2);
    assert.equal(actual[1]!.requestId, '');
    assert.equal(actual[1]!.sourceId, '');
    assert.equal(Reflect.get(actual[1]!, 'headers'), undefined);
    assert.equal(actual[0]!.message, '<script>text only</script>');
});

test('storage failures preserve memory and dismissal/undo do not duplicate or expose mutable state', () => {
    const { store } = fixture({
        storage: () => {
            throw new Error('Storage unavailable');
        },
    });
    let changes = 0;
    const unsubscribe = store.subscribe(() => changes++);
    const one = store.publish({ type: 'info', message: 'Ready' });
    assert.ok(one);
    store.publish({ type: 'success', message: 'Export completed' });
    store.list()[0]!.message = 'tampered';
    assert.equal(store.list()[0]!.message, 'Export completed');
    assert.equal(store.dismiss([one.id]).length, 1);
    const removed = store.clear();
    assert.deepEqual(store.list(), []);
    store.restore(removed.concat(removed));
    assert.equal(store.list().length, 1);
    assert.equal(changes, 5);
    unsubscribe();
    store.forget();
    assert.equal(changes, 5);
});

test('search combines with severity and grouping preserves occurrences across time gaps and separates operations', () => {
    const { store, advance } = fixture();
    store.publish({ type: 'error', title: 'Save failed', operation: 'Save category', requestId: 'ref-one' });
    advance(10);
    store.publish({ type: 'error', title: 'Save failed', operation: 'Save category', requestId: 'ref-two' });
    store.publish({ type: 'error', title: 'Save failed', operation: 'Save profile' });
    store.publish({ type: 'success', message: 'Export ready' });
    assert.equal(filterNotifications(store.list(), 'info', 'EXPORT').length, 1);
    assert.equal(filterNotifications(store.list(), 'error', 'ref-one').length, 1);
    const groups = groupNotifications(store.list());
    assert.equal(groups.length, 3);
    assert.equal(groups.find((group) => group.entries.length === 2)!.entries[0]!.requestId, 'ref-two');
    advance(60_001);
    const latest = store.publish({
        type: 'error',
        title: 'Save failed',
        operation: 'Save category',
        requestId: 'ref-three',
    });
    assert.ok(latest);
    const regrouped = groupNotifications(store.list());
    assert.equal(regrouped.length, 3);
    assert.equal(regrouped[0]!.id, latest.id);
    assert.deepEqual(
        regrouped[0]!.entries.map((entry) => entry.requestId),
        ['ref-three', 'ref-two', 'ref-one'],
    );
});

for (const [useLocks, invalidation] of [
    [false, 'empty owner'],
    [true, 'empty owner'],
    [false, 'suspend'],
    [true, 'suspend'],
]) {
    test(`${invalidation} blocks publishing and undo until identified, locks: ${useLocks}`, async () => {
        const locks = useLocks ? queuedLocks() : null;
        const { store, cache } = fixture({ locks });
        const previous = store.publish({ message: 'Previous account feedback' });
        assert.ok(previous);
        if (locks) await locks.drain();
        assert.equal(cache.size, 1);

        if (invalidation === 'empty owner') store.configure({ app: '/app/', owner: '' });
        else store.suspend();
        assert.equal(store.isActive(), false);
        assert.deepEqual(store.list(), []);
        assert.equal(store.publish({ message: 'Unidentified feedback' }), null);
        store.restore([previous]);
        store.markSeen();
        if (locks) await locks.drain();
        store.sync();
        assert.deepEqual(store.list(), []);
        assert.equal(cache.size, invalidation === 'empty owner' ? 0 : 1);

        store.configure({ app: '/app/', owner: 'account-b' });
        assert.equal(store.isActive(), true);
        const current = store.publish({ message: 'Identified feedback' });
        assert.ok(current);
        if (locks) await locks.drain();
        assert.deepEqual(store.list(), [current]);
    });
}

test('the byte ceiling evicts a continuous oldest suffix instead of retaining smaller old records', () => {
    const { store, advance } = fixture({ maxBytes: 1100 });
    store.publish({ message: 'Old small message' });
    advance(1);
    store.publish({ message: 'x'.repeat(500) });
    advance(1);
    const newest = store.publish({ message: 'n'.repeat(350) });
    assert.ok(newest);
    assert.deepEqual(
        store.list().map((entry) => entry.id),
        [newest.id],
    );
});

test('source identities deduplicate retained occurrences without suppressing independent messages', () => {
    const { store, settings, advance } = fixture();
    const input = { type: 'success' as const, message: 'Profile updated', sourceId: 'tempdata:one' };
    const first = store.publish(input);
    assert.ok(first);
    let changes = 0;
    store.subscribe(() => changes++);
    const duplicate = store.publish(input);
    assert.ok(duplicate);
    assert.deepEqual(duplicate, first);
    duplicate.message = 'Changed copy';
    assert.equal(store.list()[0]!.message, input.message);
    assert.equal(changes, 0);
    store.restore([{ ...first, id: 'duplicate-id' }]);
    assert.deepEqual(store.list(), [first]);

    const restored = createNotificationStore(settings);
    restored.configure({ app: '/app/', owner: 'account-a' });
    assert.deepEqual(restored.publish(input), first);
    restored.publish({ ...input, sourceId: 'tempdata:two' });
    restored.publish({ message: input.message });
    restored.publish({ message: input.message });
    assert.equal(restored.list().length, 4);
    advance(24 * 60 * 60 * 1000);
    assert.notEqual(restored.publish(input)!.id, first.id);
    assert.equal(restored.list().length, 1);
    restored.configure({ app: '/app/', owner: 'account-b' });
    assert.notEqual(restored.publish(input)!.id, first.id);
    assert.equal(restored.list().length, 1);
});
