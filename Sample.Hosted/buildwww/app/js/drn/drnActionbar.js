/** Opening an action panel acknowledges its attention highlight, not its count. */
export function installActionbarEvents() {
    const selector = '[data-js-actionbar-item] [data-bs-toggle="offcanvas"]';
    let contextKey = '';
    let storageKey = '';
    let owner = '';
    let attended = new Map();
    let lastRaw = null;

    function configure() {
        const marker = document.querySelector('[data-notification-context]');
        const app = marker?.dataset.notificationApp;
        const nextOwner = marker?.dataset.notificationOwner;
        if (!app || !nextOwner) return false;
        const nextKey = JSON.stringify([app, nextOwner]);
        if (nextKey !== contextKey) {
            contextKey = nextKey;
            storageKey = `drn.actionbar.v2:${nextKey}`;
            owner = nextOwner;
            attended = new Map();
            lastRaw = null;
        }
        try {
            const raw = localStorage.getItem(storageKey);
            if (raw === lastRaw) return true;
            const saved = raw && raw.length <= 65536 ? JSON.parse(raw) : null;
            lastRaw = raw;
            attended = new Map();
            if (saved?.owner === owner && Array.isArray(saved.entries))
                attended = new Map(saved.entries.slice(0, 100).filter(entry =>
                    Array.isArray(entry) && entry.length === 2 && entry.every(value => typeof value === 'string')));
        } catch { /* Blocked or invalid storage leaves attention state in memory. */ }
        return true;
    }

    function save() {
        try {
            const raw = JSON.stringify({ owner, entries: [...attended] });
            localStorage.setItem(storageKey, raw);
            lastRaw = raw;
        } catch { /* Acknowledgment still works for the current page. */ }
    }

    function version(control) {
        return control.dataset.actionbarAttentionVersion ||
            control.querySelector('[data-actionbar-count]')?.textContent?.trim() || '';
    }

    function refresh() {
        if (!configure()) return;
        for (const control of document.querySelectorAll(selector)) {
            const current = version(control);
            control.classList.toggle('text-bg-warning', !!current && current !== '0' &&
                attended.get(control.dataset.bsTarget) !== current);
        }
    }

    document.addEventListener('shown.bs.offcanvas', event => {
        const panelId = event.target?.id;
        if (!panelId || !configure()) return;
        for (const control of document.querySelectorAll(selector)) {
            if (control.dataset.bsTarget !== `#${panelId}`) continue;
            attended.set(control.dataset.bsTarget, version(control));
            if (attended.size > 100) attended.delete(attended.keys().next().value);
            control.classList.remove('text-bg-warning');
        }
        save();
    });
    document.addEventListener('htmx:load', refresh);
    document.addEventListener('drn:notification-context-changed', refresh);
    window.addEventListener('pageshow', refresh);
    window.addEventListener('storage', event => {
        if (event.key === storageKey || event.key === null) refresh();
    });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', refresh, { once: true });
    else refresh();
}
