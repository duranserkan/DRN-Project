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
        createdAt: value.createdAt, persist: value.persist !== false
    };
}

/** Bounded notification history. Storage is a cache and never a source of executable content. */
export function createNotificationStore({ storage = () => globalThis.sessionStorage, now = Date.now,
    newId = createNotificationId, maxEntries = MAX_ENTRIES,
    maxBytes = MAX_BYTES, maxAge = MAX_AGE } = {}) {
    let entries = [];
    let context = null;
    let suspended = false;
    const listeners = new Set();
    const storageKey = () => `drn.notifications.v1:${context.app}`;
    const envelope = items => ({ version: 1, owner: context?.owner, entries: items });

    function prune() {
        const cutoff = now() - maxAge;
        const ids = new Set();
        const sources = new Set();
        let full = false;
        let bytes = encoder.encode(JSON.stringify(envelope([]))).length;
        entries = entries.sort((a, b) => b.createdAt - a.createdAt).filter(entry => {
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

    function save() {
        if (!context) return;
        try {
            storage()?.setItem(storageKey(), JSON.stringify(envelope(entries.filter(entry => entry.persist))));
        } catch {
            // Notifications must still work when storage is blocked or its quota is exhausted.
            try { storage()?.removeItem(storageKey()); } catch { /* Storage can be entirely unavailable. */ }
        }
    }

    function changed() {
        prune();
        save();
        for (const listener of listeners) listener();
    }

    function configure(value) {
        const app = exact(value?.app, 300);
        const owner = exact(value?.owner, 200);
        if (!app || !owner) return false;
        if (context?.app === app && context.owner === owner && !suspended) return false;
        const previous = context;
        if (previous) {
            try { storage()?.removeItem(storageKey()); } catch { /* Memory remains authoritative. */ }
        }
        context = { app, owner };
        suspended = false;
        entries = [];
        try {
            const raw = storage()?.getItem(storageKey());
            if (raw && encoder.encode(raw).length <= maxBytes) {
                const saved = JSON.parse(raw);
                if (saved?.version === 1 && saved.owner === owner && Array.isArray(saved.entries)) {
                    entries = saved.entries.slice(0, maxEntries)
                        .map(item => normalize(item, now())).filter(item => item?.persist);
                }
            }
        } catch { /* Invalid or unavailable history is replaced with an empty cache. */ }
        changed();
        return true;
    }

    function publish(value) {
        if (suspended) return null;
        const entry = normalize({ ...value, id: newId(), createdAt: now(),
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
        const selected = new Set(ids);
        const removed = entries.filter(entry => selected.has(entry.id));
        entries = entries.filter(entry => !selected.has(entry.id));
        changed();
        return removed.map(entry => ({ ...entry }));
    }

    return {
        configure, publish, dismiss,
        isActive() { return !suspended; },
        suspend() { suspended = true; entries = []; changed(); },
        list() { prune(); return entries.map(entry => ({ ...entry })); },
        subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener); },
        clear() { return dismiss(entries.map(entry => entry.id)); },
        forget() { entries = []; changed(); },
        restore(items) {
            if (suspended) return;
            entries.push(...items.slice(0, maxEntries).map(item => normalize(item, now())).filter(Boolean));
            changed();
        },
        expire() { changed(); }
    };
}

export default createNotificationStore();
