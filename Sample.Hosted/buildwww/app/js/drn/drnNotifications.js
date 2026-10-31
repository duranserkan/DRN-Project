const MAX_ENTRIES = 1000;
const MAX_BYTES = 1024 * 1024;
const MAX_AGE = 24 * 60 * 60 * 1000;
const encoder = new TextEncoder();
const types = new Set(['error', 'warning', 'info', 'success']);

function createNotificationId() {
    if (typeof globalThis.crypto.randomUUID === 'function') return globalThis.crypto.randomUUID();
    // getRandomValues also works on HTTP origins where randomUUID is unavailable.
    const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
    return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
}

function text(value, length) {
    return typeof value === 'string' ? value.trim().slice(0, length) : '';
}

function exact(value, length) {
    return typeof value === 'string' && value.length <= length ? value : '';
}

function normalize(value, now) {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return null;
    const id = exact(value.id, 100);
    const message = text(value.message, 2000);
    const title = text(value.title, 500);
    if (!id || (!message && !title) || !types.has(value.type) ||
        !Number.isSafeInteger(value.createdAt) || value.createdAt > now || value.createdAt < 0) return null;
    return {
        id, type: value.type, title, message,
        problemType: exact(value.problemType, 2048),
        status: Number.isInteger(value.status) && value.status >= 0 && value.status <= 599 ? value.status : 0,
        requestId: exact(value.requestId, 200),
        sourceId: exact(value.sourceId, 200),
        operation: text(value.operation, 200), target: text(value.target, 200),
        createdAt: value.createdAt, persist: value.persist !== false, seen: value.seen === true
    };
}

/** Bounded notification history. Storage is a cache and never a source of executable content. */
export function createNotificationStore({ locks = globalThis.navigator?.locks,
    storage = () => locks ? globalThis.localStorage : globalThis.sessionStorage, now = Date.now,
    newId = createNotificationId, maxEntries = MAX_ENTRIES,
    maxBytes = MAX_BYTES, maxAge = MAX_AGE } = {}) {
    let entries = [];
    let context = null;
    let suspended = false;
    let lastRaw = null;
    let persisted = [];
    let generation = 0;
    let pending = 0;
    let storageFailed = false;
    const listeners = new Set();
    const storageKey = () => `drn.notifications.v1:${context.app}`;
    const envelope = items => ({ version: 1, owner: context?.owner, entries: items });

    function bounded(items) {
        const cutoff = now() - maxAge;
        const ids = new Set();
        const sources = new Set();
        let full = false;
        let bytes = encoder.encode(JSON.stringify(envelope([]))).length;
        return items.sort((a, b) => b.createdAt - a.createdAt).filter(entry => {
            if (entry.createdAt <= cutoff || ids.has(entry.id) ||
                (entry.sourceId && sources.has(entry.sourceId)) || ids.size >= maxEntries || full) return false;
            const size = encoder.encode(JSON.stringify(entry)).length + 1;
            if (bytes + size > maxBytes) { full = true; return false; }
            ids.add(entry.id);
            if (entry.sourceId) sources.add(entry.sourceId);
            bytes += size;
            return true;
        });
    }

    function prune() { entries = bounded(entries); }

    function decode(raw) {
        if (!raw || encoder.encode(raw).length > maxBytes) return null;
        const saved = JSON.parse(raw);
        return saved?.version === 1 && Array.isArray(saved.entries) ? saved : null;
    }

    function savedEntries(saved) {
        return saved?.entries.slice(0, maxEntries)
            .map(item => normalize(item, now())).filter(item => item?.persist) ?? [];
    }

    function save(initialize = false) {
        if (!context || storageFailed || (suspended && !initialize)) return;
        const next = entries.filter(entry => entry.persist);
        const before = new Map(persisted.map(entry => [entry.id, entry]));
        const retained = new Set(next.map(entry => entry.id));
        const added = next.filter(entry => !before.has(entry.id));
        const removed = new Set(persisted.filter(entry => !retained.has(entry.id)).map(entry => entry.id));
        const newlySeen = next.filter(entry => entry.seen && !before.get(entry.id)?.seen);
        const seen = new Set(newlySeen.map(entry => entry.id));
        const seenSources = new Set(newlySeen.map(entry => entry.sourceId).filter(Boolean));
        persisted = next;
        const key = storageKey();
        const owner = context.owner;
        const version = generation;
        const commit = () => {
            if (version !== generation || storageFailed || (suspended && owner)) return;
            const cache = storage();
            if (!cache) return;
            if (!owner) { cache.removeItem(key); return; }
            const currentRaw = cache.getItem(key);
            let saved;
            try { saved = decode(currentRaw); } catch { saved = null; }
            if (saved && saved.owner !== owner && !initialize) {
                suspended = true;
                entries = [];
                return;
            }
            const shared = saved?.owner === owner ? savedEntries(saved) : [];
            const remaining = shared.filter(entry => !removed.has(entry.id));
            const sources = new Set(remaining.map(entry => entry.sourceId).filter(Boolean));
            const merged = bounded([...remaining
                .map(entry => seen.has(entry.id) || seenSources.has(entry.sourceId) ? { ...entry, seen: true } : entry),
                ...added.filter(entry => !entry.sourceId || !sources.has(entry.sourceId))]);
            // Initialization only adopts the latest same-owner state. It never replays a stale snapshot.
            const raw = JSON.stringify({ version: 1, owner, entries: merged });
            cache.setItem(key, raw);
        };
        if (locks) {
            pending++;
            const finish = () => {
                if (version !== generation) return;
                pending--;
                if (!pending) readShared();
                for (const listener of listeners) listener();
            };
            try {
                Promise.resolve(locks.request(key, commit)).catch(() => {
                    if (version === generation) storageFailed = true;
                }).finally(finish);
            } catch {
                storageFailed = true;
                finish();
            }
            return;
        }
        // Without a lock manager the default storage is tab-local, so synchronous writes are safe.
        if (!owner) return;
        try {
            const cache = storage();
            if (!cache) return;
            const raw = JSON.stringify(envelope(entries.filter(entry => entry.persist)));
            cache.setItem(storageKey(), raw);
            lastRaw = raw;
        } catch {
            // Notifications must still work when storage is blocked or its quota is exhausted.
            try { storage()?.removeItem(storageKey()); lastRaw = null; } catch { /* Storage can be entirely unavailable. */ }
        }
    }

    // Pending operations own the local view until all their shared commits finish.
    function readShared() {
        if (!context?.owner || suspended || pending || storageFailed) return;
        try {
            const cache = storage();
            if (!cache) return;
            const raw = cache.getItem(storageKey());
            if (raw === lastRaw) return;
            if (raw && encoder.encode(raw).length > maxBytes) return;
            const saved = raw ? JSON.parse(raw) : null;
            if (saved && (saved.version !== 1 || !Array.isArray(saved.entries))) return;
            lastRaw = raw;
            if (saved && saved.owner !== context.owner) {
                suspended = true;
                entries = [];
                return;
            }
            const shared = saved?.entries.slice(0, maxEntries)
                .map(item => normalize(item, now())).filter(item => item?.persist) ?? [];
            entries = [...shared, ...entries.filter(entry => !entry.persist)];
            prune();
            persisted = entries.filter(entry => entry.persist);
        } catch { /* Keep current-page history when storage is unavailable or invalid. */ }
    }

    function changed(initialize = false) {
        prune();
        save(initialize);
        for (const listener of listeners) listener();
    }

    function configure(value) {
        const app = exact(value?.app, 300);
        const owner = exact(value?.owner, 200);
        if (!app) return false;
        if (context?.app === app && context.owner === owner && !suspended) return false;
        const previous = context;
        if (previous && !locks) {
            try { storage()?.removeItem(storageKey()); } catch { /* Memory remains authoritative. */ }
        }
        context = { app, owner };
        suspended = !owner;
        entries = [];
        persisted = [];
        generation++;
        pending = 0;
        lastRaw = null;
        try {
            // An unidentified authenticated user must not retain or restore any account's history.
            if (!owner && !locks) storage()?.removeItem(storageKey());
            const raw = owner ? storage()?.getItem(storageKey()) : null;
            lastRaw = raw;
            if (raw && encoder.encode(raw).length <= maxBytes) {
                const saved = JSON.parse(raw);
                if (saved?.version === 1 && saved.owner === owner && Array.isArray(saved.entries)) {
                    entries = saved.entries.slice(0, maxEntries)
                        .map(item => normalize(item, now())).filter(item => item?.persist);
                }
            }
        } catch { /* Invalid or unavailable history is replaced with an empty cache. */ }
        persisted = entries.filter(entry => entry.persist);
        changed(true);
        return true;
    }

    function publish(value) {
        readShared();
        if (suspended) return null;
        const entry = normalize({ ...value, id: newId(), createdAt: now(), seen: false,
            type: types.has(value?.type) ? value.type : 'info' }, now());
        if (!entry) return null;
        if (entry.sourceId) {
            prune();
            const existing = entries.find(item => item.sourceId === entry.sourceId);
            if (existing) return { ...existing };
        }
        entries.unshift(entry);
        changed();
        return { ...entry };
    }

    function dismiss(ids) {
        readShared();
        if (suspended) return [];
        const selected = new Set(ids);
        const removed = entries.filter(entry => selected.has(entry.id));
        entries = entries.filter(entry => !selected.has(entry.id));
        changed();
        return removed.map(entry => ({ ...entry }));
    }

    return {
        configure, publish, dismiss,
        sync() {
            readShared();
            for (const listener of listeners) listener();
        },
        markSeen() {
            readShared();
            if (suspended || !entries.some(entry => !entry.seen)) return;
            entries = entries.map(entry => ({ ...entry, seen: true }));
            changed();
        },
        isActive() { return !suspended; },
        suspend() { suspended = true; entries = []; changed(); },
        list() { prune(); return entries.map(entry => ({ ...entry })); },
        subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener); },
        clear() { readShared(); return dismiss(entries.map(entry => entry.id)); },
        forget() { readShared(); entries = []; changed(); },
        restore(items) {
            readShared();
            if (suspended) return;
            entries.push(...items.slice(0, maxEntries).map(item => normalize(item, now())).filter(Boolean));
            changed();
        },
        expire() { readShared(); changed(); }
    };
}

export default createNotificationStore();
