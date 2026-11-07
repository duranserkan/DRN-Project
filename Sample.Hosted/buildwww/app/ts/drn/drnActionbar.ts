function version(control: HTMLElement) {
    return (
        control.dataset.actionbarAttentionVersion ||
        control.querySelector('[data-actionbar-count]')?.textContent?.trim() ||
        ''
    );
}

/** Opening an action panel acknowledges its attention highlight, not its count. */
export function installActionbarEvents() {
    const selector = '[data-js-actionbar-item] [data-bs-toggle="offcanvas"]';
    let contextKey = '';
    let storageKey = '';
    let legacyKey = '';
    let owner = '';
    let attended = new Map<string, string>();
    let unsaved = new Map<string, string>();

    function configure() {
        const marker = document.querySelector<HTMLElement>('[data-notification-context]');
        const app = marker?.dataset.notificationApp;
        const nextOwner = marker?.dataset.notificationOwner;
        if (!app || !nextOwner) return false;
        const nextKey = JSON.stringify([app, nextOwner]);
        if (nextKey !== contextKey) {
            contextKey = nextKey;
            storageKey = `drn.actionbar.v3:${nextKey}:`;
            legacyKey = `drn.actionbar.v2:${nextKey}`;
            owner = nextOwner;
            attended = new Map<string, string>();
            unsaved = new Map<string, string>();
        }
        restoreLegacyAttendance();
        for (const control of document.querySelectorAll<HTMLElement>(selector)) {
            const target = control.dataset.bsTarget;
            if (!target) continue;
            try {
                const raw = localStorage.getItem(storageKey + target);
                if (raw !== null) attended.set(target, raw);
            } catch {
                /* Keep the last known acknowledgment when storage is blocked. */
            }
            if (unsaved.has(target)) attended.set(target, unsaved.get(target)!);
        }
        return true;
    }

    function restoreLegacyAttendance() {
        try {
            const raw = localStorage.getItem(legacyKey);
            const saved = raw && raw.length <= 65536 ? JSON.parse(raw) : null;
            attended = new Map<string, string>();
            if (saved?.owner === owner && Array.isArray(saved.entries))
                attended = new Map(
                    saved.entries
                        .slice(0, 100)
                        .filter(
                            (entry: unknown) =>
                                Array.isArray(entry) &&
                                entry.length === 2 &&
                                entry.every((value) => typeof value === 'string'),
                        ),
                );
        } catch {
            /* Blocked or invalid storage leaves attention state in memory. */
        }
    }

    function save(target: string, current: string) {
        try {
            // Separate keys make acknowledgments of different panels independent across tabs.
            localStorage.setItem(storageKey + target, current);
            unsaved.delete(target);
        } catch {
            unsaved.set(target, current);
        }
    }

    function refresh() {
        if (!configure()) return;
        for (const control of document.querySelectorAll<HTMLElement>(selector)) {
            const current = version(control);
            control.classList.toggle(
                'text-bg-warning',
                !!current && current !== '0' && attended.get(control.dataset.bsTarget ?? '') !== current,
            );
        }
    }

    document.addEventListener('shown.bs.offcanvas', (event) => {
        const panelId = (event.target as Element | null)?.id;
        if (!panelId || !configure()) return;
        for (const control of document.querySelectorAll<HTMLElement>(selector)) {
            if (control.dataset.bsTarget !== `#${panelId}`) continue;
            const current = version(control);
            attended.set(control.dataset.bsTarget, current);
            if (attended.size > 100) attended.delete(attended.keys().next().value!);
            control.classList.remove('text-bg-warning');
            save(control.dataset.bsTarget, current);
        }
    });
    document.addEventListener('htmx:load', refresh);
    document.addEventListener('drn:notification-context-changed', refresh);
    window.addEventListener('pageshow', refresh);
    window.addEventListener('storage', (event) => {
        if (event.key === legacyKey || (storageKey && event.key?.startsWith(storageKey)) || event.key === null)
            refresh();
    });
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', refresh, { once: true });
    else refresh();
}
