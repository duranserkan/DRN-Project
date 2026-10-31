const PANEL_ID = 'offCanvasSidebarActionCanvasNotifications';
const PAGE_SIZE = 50;
const GROUP_WINDOW = 60_000;
const FILTER_LABELS = { all: 'All', error: 'Errors', warning: 'Warnings', info: 'Info' };
const SEVERITIES = {
    error: { label: 'Error', icon: 'bi-exclamation-triangle-fill', color: 'text-danger' },
    warning: { label: 'Warning', icon: 'bi-exclamation-circle-fill', color: 'text-warning' },
    info: { label: 'Information', icon: 'bi-info-circle-fill', color: 'text-primary' },
    success: { label: 'Success', icon: 'bi-check-circle-fill', color: 'text-success' }
};

let installed = false;
let panel = null;
let filter = 'all';
let query = '';
let limit = PAGE_SIZE;
let undoEntries = [];
let undoTimer;

export function filterNotifications(entries, severity = 'all', search = '') {
    const needle = search.trim().toLocaleLowerCase();
    return entries.filter(entry => {
        const matchesSeverity = severity === 'all' || entry.type === severity ||
            (severity === 'info' && entry.type === 'success');
        const searchable = [entry.title, entry.message, entry.operation, entry.requestId].join('\n').toLocaleLowerCase();
        return matchesSeverity && (!needle || searchable.includes(needle));
    });
}

export function groupNotifications(entries) {
    const groups = [];
    const latestGroups = new Map();
    for (const entry of entries) {
        const key = JSON.stringify([entry.type, entry.problemType, entry.status, entry.operation,
            entry.target, entry.title, entry.message]);
        let group = latestGroups.get(key);
        if (!group || group.entries[0].createdAt - entry.createdAt > GROUP_WINDOW) {
            group = { id: entry.id, entries: [] };
            latestGroups.set(key, group);
            groups.push(group);
        }
        group.entries.push(entry);
    }
    return groups;
}

function store() {
    return window.DRN.Notifications;
}

function show(element, visible) {
    if (!element) return;
    element.hidden = !visible;
    element.classList.toggle('d-none', !visible);
}

function element(tag, className = '', text = '') {
    const node = document.createElement(tag);
    node.className = className;
    node.textContent = text;
    return node;
}

function button(text, action, ids) {
    const node = element('button', 'btn btn-sm btn-outline-secondary', text);
    node.type = 'button';
    node.dataset.notificationAction = action;
    if (ids) node.dataset.notificationIds = JSON.stringify(ids);
    return node;
}

function renderTime(createdAt) {
    const date = new Date(createdAt);
    const time = element('time', 'small text-body-secondary', date.toLocaleString());
    time.dateTime = date.toISOString();
    return time;
}

function renderOccurrence(entry) {
    const row = element('li', 'border-top py-2');
    row.append(renderTime(entry.createdAt));
    if (entry.requestId) {
        const reference = element('div', 'small text-break user-select-text', `Request reference: ${entry.requestId}`);
        const copy = button('Copy reference', 'copy');
        copy.dataset.notificationReference = entry.requestId;
        row.append(reference, copy);
    }
    row.append(button('Dismiss occurrence', 'dismiss', [entry.id]));
    return row;
}

function renderOccurrences(group, expanded) {
    const details = element('details');
    details.dataset.notificationDetails = group.id;
    details.open = expanded.has(group.id);
    details.append(element('summary', 'small', group.entries.length > 1
        ? `${group.entries.length} occurrences and references` : 'Details and request reference'));
    const occurrences = element('ul', 'list-unstyled mb-0');
    // Build occurrence rows only when requested. Repeated failures may fill the entire history.
    const populate = () => {
        if (!details.open || occurrences.childElementCount) return;
        let shown = 0;
        const more = button('Load more occurrences', 'occurrences');
        occurrences.append(more);
        const appendPage = () => {
            const moveFocus = document.activeElement === more;
            let firstNewRow;
            for (const occurrence of group.entries.slice(shown, shown + PAGE_SIZE)) {
                const row = renderOccurrence(occurrence);
                firstNewRow ??= row;
                occurrences.insertBefore(row, more);
            }
            shown += PAGE_SIZE;
            if (moveFocus && firstNewRow) {
                firstNewRow.tabIndex = -1;
                firstNewRow.focus();
            }
            if (shown >= group.entries.length) more.remove();
        };
        more.addEventListener('click', appendPage);
        appendPage();
    };
    details.addEventListener('toggle', populate);
    details.append(occurrences);
    populate();
    return details;
}

function renderGroup(group, expanded) {
    const entry = group.entries[0];
    const severity = SEVERITIES[entry.type] || SEVERITIES.info;
    const row = element('li', 'border rounded p-3 mb-2');
    row.dataset.notificationGroup = group.id;
    const heading = element('div', 'd-flex align-items-start gap-2');
    const icon = element('i', `bi ${severity.icon} ${severity.color}`);
    icon.setAttribute('aria-hidden', 'true');
    heading.append(icon, element('h3', 'h6 mb-1 text-break', entry.title || severity.label));
    row.append(heading, element('span', `small ${severity.color}`, severity.label));
    if (entry.message) {
        const message = element('p', 'small text-break user-select-text my-2', entry.message);
        message.style.whiteSpace = 'pre-line';
        row.append(message);
    }
    if (entry.operation) row.append(element('p', 'small text-body-secondary mb-2', entry.operation));
    const summary = element('div', 'd-flex flex-wrap align-items-center gap-2 mb-2');
    summary.append(renderTime(entry.createdAt), button(group.entries.length > 1 ? 'Dismiss group' : 'Dismiss',
        'dismiss', group.entries.map(item => item.id)));
    row.append(summary, renderOccurrences(group, expanded));
    return row;
}

function panelIsOpen() {
    return panel?.matches('.show, .showing, .hiding') ?? false;
}

function updateTriggers(entries) {
    const visible = entries.length > 0 || panelIsOpen();
    const color = entries.some(item => item.type === 'error') ? 'bg-danger' :
        entries.some(item => item.type === 'warning') ? 'bg-warning text-dark' : 'bg-primary';
    for (const trigger of document.querySelectorAll('[data-notification-trigger]')) {
        show(trigger, visible);
        const control = trigger.querySelector('[data-notification-open]');
        control?.setAttribute('aria-label', `Notifications, ${entries.length} retained`);
        const badge = trigger.querySelector('[data-notification-count]');
        if (!badge) continue;
        badge.textContent = String(entries.length);
        badge.className = `position-absolute top-0 start-100 translate-middle badge rounded-pill ${color}`;
    }
}

function refresh(force = false) {
    panel = document.getElementById(PANEL_ID);
    const entries = store().list();
    updateTriggers(entries);
    if (!panel) return;

    const search = panel.querySelector('#drn-notification-search');
    if (search.value !== query) search.value = query;
    for (const control of panel.querySelectorAll('[data-notification-filter]')) {
        const severity = control.dataset.notificationFilter;
        control.textContent = `${FILTER_LABELS[severity]} (${filterNotifications(entries, severity).length})`;
        control.setAttribute('aria-pressed', String(severity === filter));
        control.classList.toggle('btn-primary', severity === filter);
        control.classList.toggle('btn-outline-secondary', severity !== filter);
    }
    const filtered = filterNotifications(entries, filter, query);
    const groups = groupNotifications(filtered);
    const list = panel.querySelector('#drn-notification-list');
    const expanded = new Set(Array.from(list.querySelectorAll('details[open]'), node => node.dataset.notificationDetails));
    const focusedInList = list.contains(document.activeElement);
    const result = panel.querySelector('#drn-notification-result');
    result.textContent = entries.length === 0 ? 'No notifications.' : filtered.length === 0
        ? 'No matching notifications.' : `${filtered.length} notifications in ${groups.length} groups. Newest first.`;
    panel.querySelector('[data-notification-clear-all]').disabled = entries.length === 0;
    panel.querySelector('[data-notification-clear-filtered]').disabled = filtered.length === 0;
    const loadMore = panel.querySelector('[data-notification-load-more]');
    if (groups.length <= limit && document.activeElement === loadMore) search.focus({ preventScroll: true });
    show(loadMore, groups.length > limit);
    show(panel.querySelector('[data-notification-undo-region]'), undoEntries.length > 0);
    // Incoming background messages must not replace the control being used by the keyboard user.
    if (focusedInList && !force) return;
    const fragment = document.createDocumentFragment();
    for (const group of groups.slice(0, limit)) fragment.append(renderGroup(group, expanded));
    list.replaceChildren(fragment);
    if (focusedInList) search.focus({ preventScroll: true });
}

function forgetUndo() {
    clearTimeout(undoTimer);
    undoEntries = [];
    if (document.activeElement?.matches('[data-notification-undo]'))
        document.getElementById('drn-notification-search')?.focus();
}

function announce(message) {
    const live = document.getElementById('drn-notification-polite');
    if (live) live.textContent = message;
}

async function handleClick(event) {
    if (!(event.target instanceof Element)) return;
    const control = event.target.closest('button');
    if (!control || !panel?.contains(control)) return;
    if (control.hasAttribute('data-notification-filter')) {
        filter = control.dataset.notificationFilter;
        limit = PAGE_SIZE;
    } else if (control.hasAttribute('data-notification-load-more')) {
        limit += PAGE_SIZE;
    } else if (control.hasAttribute('data-notification-clear-all')) {
        panel.querySelector('#drn-notification-search').focus();
        forgetUndo();
        undoEntries = store().clear();
        undoTimer = setTimeout(() => { forgetUndo(); refresh(); }, 10_000);
        announce('Notifications cleared. Undo is available for ten seconds.');
    } else if (control.hasAttribute('data-notification-clear-filtered')) {
        panel.querySelector('#drn-notification-search').focus();
        store().dismiss(filterNotifications(store().list(), filter, query).map(entry => entry.id));
        announce('Filtered notifications dismissed.');
    } else if (control.hasAttribute('data-notification-undo')) {
        const entries = undoEntries;
        forgetUndo();
        store().restore(entries);
        announce('Notifications restored.');
    } else if (control.dataset.notificationAction === 'dismiss') {
        store().dismiss(JSON.parse(control.dataset.notificationIds));
        announce('Notification dismissed.');
    } else if (control.dataset.notificationAction === 'copy') {
        try {
            await navigator.clipboard.writeText(control.dataset.notificationReference);
            announce('Request reference copied.');
        } catch {
            announce('Copy unavailable. Select and copy the request reference.');
        }
        return;
    } else {
        return;
    }
    refresh(true);
}

function hidePanel(node, dispose = false) {
    const instance = window.bootstrap?.Offcanvas.getInstance(node);
    if (!instance) return;
    const finish = () => { if (dispose) instance.dispose(); };
    if (node.matches('.show, .showing, .hiding')) {
        node.addEventListener('hidden.bs.offcanvas', finish, { once: true });
        // Let Bootstrap finish its own queued callbacks before disposing the instance.
        if (node.classList.contains('showing'))
            node.addEventListener('shown.bs.offcanvas', () => instance.hide(), { once: true });
        else if (!node.classList.contains('hiding'))
            instance.hide();
    } else {
        finish();
    }
}

function cleanup(event) {
    const removed = event.detail?.elt;
    if (!panel || !(removed instanceof Element) || (removed !== panel && !removed.contains(panel))) return;
    hidePanel(panel, true);
    panel = null;
}

function install() {
    if (installed) return;
    installed = true;
    store().subscribe(() => refresh());
    document.addEventListener('click', handleClick);
    document.addEventListener('input', event => {
        if (event.target instanceof HTMLInputElement && event.target.id === 'drn-notification-search') {
            query = event.target.value;
            limit = PAGE_SIZE;
            refresh();
        }
    });
    document.addEventListener('hidden.bs.offcanvas', event => {
        if (event.target?.id !== PANEL_ID) return;
        refresh();
        if (store().list().length === 0)
            document.getElementById('drn-main-content')?.focus({ preventScroll: true });
    });
    document.addEventListener('focusout', event => {
        if (event.target instanceof Element && event.target.closest('#drn-notification-list'))
            setTimeout(() => refresh(), 0);
    });
    document.addEventListener('htmx:beforeCleanupElement', cleanup);
    document.addEventListener('drn:notification-context-changed', () => {
        if (panel) hidePanel(panel);
        forgetUndo();
        filter = 'all';
        query = '';
        limit = PAGE_SIZE;
        refresh(true);
    });
    refresh();
}

export default { install, refresh };
