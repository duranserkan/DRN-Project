import type { DrnNotification } from '../../../types/drnNotifications.types';

interface NotificationGroup {
    id: string;
    entries: DrnNotification[];
}

const PANEL_ID = 'offCanvasSidebarActionCanvasNotifications';
const PAGE_SIZE = 50;
const FILTER_LABELS = { all: 'All', error: 'Errors', warning: 'Warnings', info: 'Info' };
const FILTER_COLORS = { all: 'primary', error: 'danger', warning: 'warning', info: 'info' };
const SEVERITIES = {
    error: { label: 'Error', icon: 'bi-exclamation-triangle-fill', color: 'text-danger' },
    warning: { label: 'Warning', icon: 'bi-exclamation-circle-fill', color: 'text-warning' },
    info: { label: 'Information', icon: 'bi-info-circle-fill', color: 'text-primary' },
    success: { label: 'Success', icon: 'bi-check-circle-fill', color: 'text-success' }
};

let installed = false;
let panel: HTMLElement | null = null;
let filter = 'all';
let query = '';
let limit = PAGE_SIZE;
let undoEntries: DrnNotification[] = [];
let undoTimer: ReturnType<typeof setTimeout> | undefined;
let detailsSequence = 0;

function matchesSeverity(entry: DrnNotification, severity: string) {
    return severity === 'all' || entry.type === severity ||
        (severity === 'info' && entry.type === 'success');
}

export function filterNotifications(entries: DrnNotification[], severity = 'all', search = '') {
    const needle = search.trim().toLocaleLowerCase();
    return entries.filter(entry => {
        const severityMatches = matchesSeverity(entry, severity);
        if (!severityMatches || !needle) return severityMatches;
        const searchable = [entry.title, entry.message, entry.operation, entry.requestId, entry.clientErrorId,
            entry.clientErrorDetails].join('\n').toLocaleLowerCase();
        return searchable.includes(needle);
    });
}

export function groupNotifications(entries: DrnNotification[]) {
    const groups: NotificationGroup[] = [];
    const groupsByKey = new Map<string, NotificationGroup>();
    for (const entry of entries) {
        const key = JSON.stringify([entry.type, entry.problemType, entry.status, entry.operation,
            entry.target, entry.title, entry.message]);
        let group = groupsByKey.get(key);
        if (!group) {
            group = { id: entry.id, entries: [] };
            groupsByKey.set(key, group);
            groups.push(group);
        }
        group.entries.push(entry);
    }
    return groups;
}

function store() {
    return window.DRN.Notifications;
}

function setVisibility(element: HTMLElement | null, visible: boolean) {
    if (!element) return;
    element.hidden = !visible;
    element.classList.toggle('d-none', !visible);
}

function createElement<K extends keyof HTMLElementTagNameMap>(tag: K, className = '', text = '') {
    const node = document.createElement(tag);
    node.className = className;
    node.textContent = text;
    return node;
}

function createActionButton(text: string, action: string, ids?: string[]) {
    const node = createElement('button', 'btn btn-sm btn-outline-secondary', text);
    node.type = 'button';
    node.dataset.notificationAction = action;
    if (ids) node.dataset.notificationIds = JSON.stringify(ids);
    return node;
}

function renderTime(createdAt: number) {
    const date = new Date(createdAt);
    const time = createElement('time', 'small text-body-secondary', date.toLocaleString());
    time.dateTime = date.toISOString();
    return time;
}

function renderDiagnosticDetails(details: string) {
    const diagnostic = createElement('pre', 'small text-break user-select-text');
    diagnostic.style.whiteSpace = 'pre-wrap';
    let inStack = false;
    for (const [index, line] of details.split('\n').entries()) {
        if (index) diagnostic.append('\n');
        const label: RegExpMatchArray | null = inStack ? null : line.match(/^(Message|Source|Line|Page|Stack):/);
        if (!label) { diagnostic.append(line); continue; }
        diagnostic.append(createElement('strong', '', label[0]));
        const value = line.slice(label[0].length);
        const location = label[1] === 'Line' && value.match(/^( -?\d+, )(column:)( -?\d+)$/);
        if (location) diagnostic.append(location[1]!, createElement('strong', '', location[2]), location[3]!);
        else diagnostic.append(value);
        inStack = label[1] === 'Stack';
    }
    return diagnostic;
}

function renderOccurrence(entry: DrnNotification) {
    const row = createElement('li', 'border-top py-2');
    row.append(renderTime(entry.createdAt));
    if (entry.clientErrorId && entry.clientErrorDetails) {
        row.append(createElement('h4', 'h6 mt-2', 'Client-side error details'),
            renderDiagnosticDetails(entry.clientErrorDetails));
    }
    for (const [label, id] of [['Request reference', entry.requestId], ['Client error reference', entry.clientErrorId]]) {
        if (!id) continue;
        const reference = createElement('div', 'small text-break user-select-text', `${label}: ${id}`);
        const copy = createActionButton('Copy reference', 'copy');
        copy.dataset.notificationReference = id;
        row.append(reference, createElement('p', 'small text-body-secondary mb-2',
            'Share this reference with support so they can investigate the error.'), copy);
    }
    row.append(createActionButton('Dismiss occurrence', 'dismiss', [entry.id]));
    return row;
}

function renderOccurrences(group: NotificationGroup, expanded: ReadonlySet<string | undefined>) {
    const details = createElement('div');
    details.dataset.notificationDetails = group.id;
    const toggle = createActionButton('Show details', 'details');
    const occurrences = createElement('div');
    occurrences.id = `drn-notification-occurrences-${++detailsSequence}`;
    toggle.setAttribute('aria-controls', occurrences.id);
    const setExpanded = (open: boolean) => {
        details.dataset.expanded = String(open);
        toggle.setAttribute('aria-expanded', String(open));
        toggle.textContent = open ? 'Hide details' : 'Show details';
        setVisibility(occurrences, open);
    };
    setExpanded(expanded.has(group.id));
    details.append(toggle);
    if (group.entries.length > 1)
        details.append(createElement('span', 'small ms-2', `${group.entries.length} occurrences`));
    // Build occurrence rows only when requested. Repeated failures may fill the entire history.
    const populate = () => {
        if (occurrences.hidden || occurrences.childElementCount) return;
        let shown = 0;
        const list = createElement('ul', 'list-unstyled mb-0');
        const more = createActionButton('Load more occurrences', 'occurrences');
        occurrences.append(list, more);
        const appendPage = () => {
            const moveFocus = document.activeElement === more;
            let firstNewRow;
            for (const occurrence of group.entries.slice(shown, shown + PAGE_SIZE)) {
                const row = renderOccurrence(occurrence);
                firstNewRow ??= row;
                list.append(row);
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
    toggle.addEventListener('click', () => {
        setExpanded(Boolean(occurrences.hidden));
        populate();
    });
    details.append(occurrences);
    populate();
    return details;
}

function renderGroup(group: NotificationGroup, expanded: ReadonlySet<string | undefined>) {
    const entry = group.entries[0]!;
    const severity = SEVERITIES[entry.type] || SEVERITIES.info;
    const row = createElement('li', 'border rounded p-3 mb-2');
    row.dataset.notificationGroup = group.id;
    const heading = createElement('div', 'd-flex align-items-start gap-2');
    const icon = createElement('i', `bi ${severity.icon} ${severity.color}`);
    icon.setAttribute('aria-hidden', 'true');
    heading.append(icon, createElement('h3', 'h6 mb-1 text-break', entry.title || severity.label));
    row.append(heading, createElement('span', `small ${severity.color}`, severity.label));
    if (entry.message) {
        const message = createElement('p', 'small text-break user-select-text my-2', entry.message);
        message.style.whiteSpace = 'pre-line';
        row.append(message);
    }
    if (entry.operation) row.append(createElement('p', 'small text-body-secondary mb-2', entry.operation));
    const summary = createElement('div', 'd-flex flex-wrap align-items-center gap-2 mb-2');
    summary.append(renderTime(entry.createdAt), createActionButton(group.entries.length > 1 ? 'Dismiss group' : 'Dismiss',
        'dismiss', group.entries.map(item => item.id)));
    row.append(summary, renderOccurrences(group, expanded));
    return row;
}

function updateTriggers(entries: DrnNotification[]) {
    const unseen = entries.filter(entry => !entry.seen);
    for (const trigger of document.querySelectorAll('[data-notification-trigger]')) {
        const control = trigger.querySelector('[data-notification-open]');
        control?.setAttribute('aria-label', unseen.length ? `Notifications, ${unseen.length} unseen` : 'Notifications');
        control?.classList.toggle('text-bg-warning', unseen.length > 0);
        const badge = trigger.querySelector<HTMLElement>('[data-notification-count]');
        if (!badge) continue;
        badge.textContent = String(unseen.length);
        badge.className = 'position-absolute top-0 start-100 translate-middle badge rounded-pill text-bg-danger';
        setVisibility(badge, unseen.length > 0);
    }
}

function updateFilters(panel: HTMLElement, entries: DrnNotification[]) {
    for (const control of panel.querySelectorAll<HTMLButtonElement>('[data-notification-filter]')) {
        const severity = control.dataset.notificationFilter as keyof typeof FILTER_LABELS;
        const count = filterNotifications(entries, severity).length;
        const selected = severity === filter;
        control.textContent = `${FILTER_LABELS[severity]} (${count})`;
        control.setAttribute('aria-pressed', String(selected));
        for (const color of Object.values(FILTER_COLORS))
            control.classList.remove(`btn-${color}`, `btn-outline-${color}`);
        control.classList.remove('btn-outline-secondary');
        let colorClass = 'btn-outline-secondary';
        if (selected) colorClass = `btn-${FILTER_COLORS[severity]}`;
        else if (count > 0) colorClass = `btn-outline-${FILTER_COLORS[severity]}`;
        control.classList.add(colorClass);
    }
}

function refresh(force = false) {
    panel = document.getElementById(PANEL_ID);
    const entries = store().list();
    updateTriggers(entries);
    if (!panel) return;
    if (!force && !panel.matches('.show, .showing')) return;

    const search = panel.querySelector<HTMLInputElement>('#drn-notification-search')!;
    if (search.value !== query) search.value = query;
    updateFilters(panel, entries);
    const filtered = filterNotifications(entries, filter, query);
    const groups = groupNotifications(filtered);
    const list = panel.querySelector<HTMLElement>('#drn-notification-list')!;
    const expanded = new Set(Array.from(list.querySelectorAll<HTMLElement>('[data-notification-details][data-expanded="true"]'),
        node => node.dataset.notificationDetails));
    const focusedInList = list.contains(document.activeElement);
    const result = panel.querySelector<HTMLElement>('#drn-notification-result')!;
    if (entries.length === 0) result.textContent = 'No notifications.';
    else if (filtered.length === 0) result.textContent = 'No matching notifications.';
    else result.textContent = `${filtered.length} notifications in ${groups.length} groups. Newest first.`;
    panel.querySelector<HTMLButtonElement>('[data-notification-clear-all]')!.disabled = entries.length === 0;
    panel.querySelector<HTMLButtonElement>('[data-notification-clear-filtered]')!.disabled = filtered.length === 0;
    const loadMore = panel.querySelector<HTMLButtonElement>('[data-notification-load-more]');
    if (groups.length <= limit && document.activeElement === loadMore) search.focus({ preventScroll: true });
    setVisibility(loadMore, groups.length > limit);
    setVisibility(panel.querySelector<HTMLElement>('[data-notification-undo-region]'), undoEntries.length > 0);
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

function announce(message: string) {
    const live = document.getElementById('drn-notification-polite');
    if (live) live.textContent = message;
}

async function copyReference(reference: string) {
    try {
        await navigator.clipboard.writeText(reference);
        announce('Error reference copied. Share it with support so they can investigate the error.');
    } catch {
        announce('Copy unavailable. Select and copy the error reference.');
    }
}

function handleClick(event: Event) {
    if (!(event.target instanceof Element)) return;
    const control = event.target.closest('button');
    if (!control || !panel || !panel.contains(control)) return;
    if (Object.hasOwn(control.dataset, 'notificationFilter')) {
        filter = control.dataset.notificationFilter!;
        limit = PAGE_SIZE;
    } else if (Object.hasOwn(control.dataset, 'notificationLoadMore')) {
        limit += PAGE_SIZE;
    } else if (Object.hasOwn(control.dataset, 'notificationClearAll')) {
        panel.querySelector<HTMLInputElement>('#drn-notification-search')!.focus();
        forgetUndo();
        undoEntries = store().clear();
        undoTimer = setTimeout(() => { forgetUndo(); refresh(); }, 10_000);
        announce('Notifications cleared. Undo is available for ten seconds.');
    } else if (Object.hasOwn(control.dataset, 'notificationClearFiltered')) {
        panel.querySelector<HTMLInputElement>('#drn-notification-search')!.focus();
        store().dismiss(filterNotifications(store().list(), filter, query).map(entry => entry.id));
        announce('Filtered notifications dismissed.');
    } else if (Object.hasOwn(control.dataset, 'notificationUndo')) {
        const entries = undoEntries;
        forgetUndo();
        store().restore(entries);
        announce('Notifications restored.');
    } else if (control.dataset.notificationAction === 'dismiss') {
        store().dismiss(JSON.parse(control.dataset.notificationIds!));
        announce('Notification dismissed.');
    } else if (control.dataset.notificationAction === 'copy') {
        void copyReference(control.dataset.notificationReference!);
        return;
    } else {
        return;
    }
    refresh(true);
}

function hidePanel(node: HTMLElement, dispose = false) {
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

function cleanup(event: DocumentEventMap['htmx:beforeCleanupElement']) {
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
    document.addEventListener('shown.bs.offcanvas', event => {
        if ((event.target as Element | null)?.id !== PANEL_ID) return;
        store().markSeen();
    });
    document.addEventListener('show.bs.offcanvas', event => {
        if ((event.target as Element | null)?.id === PANEL_ID) refresh(true);
    });
    document.addEventListener('hidden.bs.offcanvas', event => {
        if ((event.target as Element | null)?.id !== PANEL_ID) return;
        refresh();
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
