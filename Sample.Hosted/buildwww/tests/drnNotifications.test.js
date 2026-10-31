import test from 'node:test';
import assert from 'node:assert/strict';
import { createNotificationStore } from '../app/js/drn/drnNotifications.js';
import { filterNotifications, groupNotifications } from '../app/js/drn/drnNotificationPanel.js';

function fixture(options = {}) {
    const cache = new Map();
    let timestamp = 100_000_000;
    let sequence = 0;
    const storage = {
        getItem: key => cache.get(key) ?? null,
        setItem: (key, value) => cache.set(key, value),
        removeItem: key => cache.delete(key)
    };
    const settings = { storage: () => storage, now: () => timestamp, newId: () => `id-${++sequence}`, ...options };
    const store = createNotificationStore(settings);
    store.configure({ app: '/app/', owner: 'account-a' });
    return { store, cache, settings, storage, advance: duration => { timestamp += duration; } };
}

test('default IDs preserve publishing, persistence and individual dismissal without randomUUID', () => {
    const originalCrypto = Object.getOwnPropertyDescriptor(globalThis, 'crypto');
    let sequence = 0;
    Object.defineProperty(globalThis, 'crypto', { configurable: true, value: {
        getRandomValues(bytes) {
            bytes.fill(++sequence);
            return bytes;
        }
    } });
    try {
        const { store, settings, advance } = fixture({ newId: undefined });
        const first = store.publish({ type: 'error', message: 'Save failed' });
        advance(1);
        const second = store.publish({ type: 'success', message: 'Save completed' });
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
        else delete globalThis.crypto;
    }
});

test('default IDs retain native randomUUID when it is available', () => {
    const originalCrypto = Object.getOwnPropertyDescriptor(globalThis, 'crypto');
    const id = '8cdb9d4f-7a85-4399-988f-15ae79bc3ed8';
    Object.defineProperty(globalThis, 'crypto', { configurable: true, value: {
        randomUUID() {
            assert.equal(this, globalThis.crypto);
            return id;
        },
        getRandomValues() { assert.fail('The native UUID path must not use the fallback.'); }
    } });
    try {
        const { store } = fixture({ newId: undefined });
        const entry = store.publish({ message: 'Ready' });
        assert.equal(entry.id, id);
        assert.deepEqual(store.list(), [entry]);
    } finally {
        if (originalCrypto) Object.defineProperty(globalThis, 'crypto', originalCrypto);
        else delete globalThis.crypto;
    }
});

test('history survives reload, excludes memory-only entries and isolates application/account boundaries', () => {
    const { store, settings, cache } = fixture();
    const publicEntry = store.publish({ type: 'error', title: 'Save failed', requestId: 'public-reference' });
    store.publish({ type: 'warning', message: 'Memory-only detail', persist: false });
    assert.equal(store.list().length, 2);
    assert.ok(![...cache.values()][0].includes('Memory-only detail'));
    const restored = createNotificationStore(settings);
    restored.configure({ app: '/app/', owner: 'account-a' });
    assert.deepEqual(restored.list(), [publicEntry]);
    restored.configure({ app: '/app/', owner: 'account-b' });
    assert.deepEqual(restored.list(), []);
    restored.publish({ type: 'info', message: 'Account B' });
    restored.configure({ app: '/other/', owner: 'account-b' });
    assert.deepEqual(restored.list(), []);
});

test('count, byte and age ceilings apply before presentation grouping', () => {
    const { store, advance } = fixture();
    for (let index = 0; index < 1005; index++) {
        advance(1);
        store.publish({ type: 'error', message: 'Repeated failure' });
    }
    assert.equal(store.list().length, 1000);
    assert.equal(store.list().at(-1).id, 'id-6');
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
    assert.equal(bounded.store.list()[0].id, 'id-10');
});

test('corrupt, oversized, duplicate, future and unsafe restored fields cannot enter history', () => {
    const { settings, storage, store } = fixture();
    const entry = store.publish({ type: 'error', message: '<script>text only</script>' });
    const key = 'drn.notifications.v1:/app/';
    const restore = value => {
        storage.setItem(key, value);
        const next = createNotificationStore(settings);
        next.configure({ app: '/app/', owner: 'account-a' });
        return next.list();
    };
    assert.deepEqual(restore('{'), []);
    assert.deepEqual(restore('x'.repeat(1024 * 1024 + 1)), []);
    const entries = [entry, entry, { ...entry, id: 'future', createdAt: entry.createdAt + 1 },
        { ...entry, id: 'bad-type', type: '__proto__' },
        { ...entry, id: 'fields', requestId: 'r'.repeat(201), sourceId: 's'.repeat(201),
            headers: { authorization: 'secret' } }];
    const actual = restore(JSON.stringify({ version: 1, owner: 'account-a', entries }));
    assert.equal(actual.length, 2);
    assert.equal(actual[1].requestId, '');
    assert.equal(actual[1].sourceId, '');
    assert.equal(actual[1].headers, undefined);
    assert.equal(actual[0].message, '<script>text only</script>');
});

test('storage failures preserve memory and dismissal/undo do not duplicate or expose mutable state', () => {
    const { store } = fixture({ storage: () => { throw new Error('Storage unavailable'); } });
    let changes = 0;
    const unsubscribe = store.subscribe(() => changes++);
    const one = store.publish({ type: 'info', message: 'Ready' });
    store.publish({ type: 'success', message: 'Export completed' });
    store.list()[0].message = 'tampered';
    assert.equal(store.list()[0].message, 'Export completed');
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

test('search combines with severity and grouping preserves distinct operations, references and time windows', () => {
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
    assert.equal(groups.find(group => group.entries.length === 2).entries[0].requestId, 'ref-two');
    advance(60_001);
    store.publish({ type: 'error', title: 'Save failed', operation: 'Save category' });
    assert.equal(groupNotifications(store.list()).length, 4);
});

test('account invalidation blocks publishing and undo until a fresh owner is configured', () => {
    const { store } = fixture();
    const previous = store.publish({ type: 'error', message: 'Account A' });
    store.suspend();
    assert.equal(store.isActive(), false);
    assert.equal(store.publish({ type: 'info', message: 'Outstanding response' }), null);
    store.restore([previous]);
    assert.deepEqual(store.list(), []);
    store.configure({ app: '/app/', owner: 'account-b' });
    assert.equal(store.isActive(), true);
    assert.deepEqual(store.list(), []);
});

test('the byte ceiling evicts a continuous oldest suffix instead of retaining smaller old records', () => {
    const { store, advance } = fixture({ maxBytes: 1100 });
    store.publish({ message: 'Old small message' });
    advance(1);
    store.publish({ message: 'x'.repeat(500) });
    advance(1);
    const newest = store.publish({ message: 'n'.repeat(350) });
    assert.deepEqual(store.list().map(entry => entry.id), [newest.id]);
});

test('source identities deduplicate retained occurrences without suppressing independent messages', () => {
    const { store, settings, advance } = fixture();
    const input = { type: 'success', message: 'Profile updated', sourceId: 'tempdata:one' };
    const first = store.publish(input);
    let changes = 0;
    store.subscribe(() => changes++);
    const duplicate = store.publish(input);
    assert.deepEqual(duplicate, first);
    duplicate.message = 'Changed copy';
    assert.equal(store.list()[0].message, input.message);
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
    assert.notEqual(restored.publish(input).id, first.id);
    assert.equal(restored.list().length, 1);
    restored.configure({ app: '/app/', owner: 'account-b' });
    assert.notEqual(restored.publish(input).id, first.id);
    assert.equal(restored.list().length, 1);
});
