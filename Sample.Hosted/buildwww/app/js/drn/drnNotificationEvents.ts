import { mediaType } from './drnHttp.ts';

import type { HtmxBeforeSwapDetails, HtmxResponseInfo } from 'htmx.org';
import type notificationPanel from './drnNotificationPanel.ts';

type ResponseDetail = HtmxResponseInfo & { elt: Element };
type ResponseEvent = CustomEvent<ResponseDetail>;
type AccountContext = Parameters<IDRN['Notifications']['configure']>[0];

declare global {
    interface DocumentEventMap {
        'htmx:beforeOnLoad': ResponseEvent;
        'htmx:beforeRequest': ResponseEvent;
        'htmx:beforeSwap': CustomEvent<ResponseDetail & HtmxBeforeSwapDetails>;
        'htmx:responseError': ResponseEvent;
        'htmx:sendError': ResponseEvent;
        'htmx:timeout': ResponseEvent;
        'htmx:afterRequest': ResponseEvent;
        'htmx:beforeCleanupElement': CustomEvent<{ elt: Element }>;
        showToast: CustomEvent<Parameters<IDRN['Toast']['show']>[0]>;
    }
}

function syncHistory() {
    DRN.Notifications.sync();
}

function isValidationFragment(xhr: XMLHttpRequest, contentType: string) {
    return xhr.status === 422 && contentType === 'text/html' &&
        xhr.getResponseHeader('X-DRN-Validation') === 'true';
}

function affectsUserAction(detail: ResponseDetail) {
    const element = detail?.requestConfig?.elt || detail?.elt;
    const explicit = element?.closest?.<HTMLElement>('[data-notification-user-action]')?.dataset.notificationUserAction;
    if (explicit !== undefined) return explicit === 'true';
    return detail?.requestConfig?.triggeringEvent?.isTrusted === true;
}

function requestContext(detail: ResponseDetail) {
    const element = detail?.requestConfig?.elt || detail?.elt;
    let operation = 'Request';
    try {
        const path = new URL(detail?.requestConfig?.path || location.pathname, location.href).pathname;
        operation = `${(detail?.requestConfig?.verb || 'GET').toUpperCase()} ${path}`;
    } catch { /* Do not retain unparseable URLs or query strings. */ }
    return {
        element, target: detail?.target,
        operation: element?.closest?.<HTMLElement>('[data-notification-operation]')?.dataset.notificationOperation || operation,
        persist: element?.closest?.<HTMLElement>('[data-notification-persist]')?.dataset.notificationPersist !== 'false',
        focus: !!detail?.requestConfig?.triggeringEvent?.isTrusted
    };
}

/** Retain the existing inline status without adding a second transient announcement. */
export function importStatusMessages(root: ParentNode, store: IDRN['Notifications']) {
    if (!store.isActive()) return;
    for (const element of root.querySelectorAll<HTMLElement>('[data-notification-status]')) {
        if (element.dataset.notificationRecorded === 'true') continue;
        const source = element.dataset.notificationStatus;
        const message = element.querySelector('[data-notification-status-message]')?.textContent?.trim();
        if (!/^[a-f0-9]{32}$/.test(source || '') || !message) continue;
        const entry = store.publish({ type: 'success', title: 'Status', message, sourceId: `tempdata:${source}`,
            persist: element.closest<HTMLElement>('[data-notification-persist]')?.dataset.notificationPersist !== 'false' });
        // Keep dismissal effective when HTMX or pageshow processes this same DOM again.
        if (entry) element.dataset.notificationRecorded = 'true';
    }
}

/** HTTP failures are handled once per XHR, even if responseError follows beforeSwap. */
export function installNotificationEvents(panel: typeof notificationPanel) {
    const handled = new WeakSet<XMLHttpRequest>();
    let currentContext: AccountContext | null = null;
    let channel: BroadcastChannel | null = null;
    let expiryTimer: number | null = null;
    let loadingDetail: ResponseDetail | null = null;
    let activeMarker: HTMLElement | null = null;
    let epoch = 0;
    let observedActive = true;
    const requestEpochs = new WeakMap<XMLHttpRequest, number>();

    function stale(detail: ResponseDetail) {
        return !DRN.Notifications.isActive() ||
            (detail?.xhr && requestEpochs.has(detail.xhr) && requestEpochs.get(detail.xhr) !== epoch);
    }

    function shouldClearProblem(detail: ResponseDetail) {
        return detail?.xhr?.status >= 200 && detail.xhr.status < 300 &&
            !handled.has(detail.xhr) && !stale(detail);
    }

    function showAccountChange() {
        const region = document.getElementById('drn-problem-summary');
        if (!region) return;
        region.hidden = false;
        region.classList.remove('d-none');
        const alert = document.createElement('div');
        alert.className = 'alert alert-warning';
        alert.setAttribute('role', 'alert');
        alert.textContent = 'Your account changed in another tab. Reload this page to continue. ';
        const reload = document.createElement('button');
        reload.type = 'button';
        reload.className = 'btn btn-sm btn-outline-dark';
        reload.textContent = 'Reload page';
        reload.addEventListener('click', () => location.reload());
        alert.appendChild(reload);
        region.replaceChildren(alert);
    }

    function contextChanged() {
        DRN.Problem.clearAll();
        DRN.Toast.cleanup(document.body);
        document.dispatchEvent(new Event('drn:notification-context-changed'));
    }

    function configure() {
        const marker = document.querySelector<HTMLElement>('[data-notification-context]');
        if (!marker) return;
        if (!DRN.Notifications.isActive() && marker === activeMarker) return;
        const context = { app: marker.dataset.notificationApp ?? '', owner: marker.dataset.notificationOwner ?? '' };
        activeMarker = marker;
        if (!DRN.Notifications.configure(context)) return;
        observedActive = DRN.Notifications.isActive();
        // HTMX load triggers can start requests before the first DOMContentLoaded configuration.
        if (currentContext) epoch++;
        currentContext = context;
        contextChanged();
        channel?.close();
        channel = null;
        ensureResources()?.postMessage({ type: 'account', owner: context.owner });
    }

    function ensureResources() {
        if (expiryTimer === null)
            expiryTimer = window.setInterval(() => DRN.Notifications.expire(), 60000);
        if (channel || !currentContext) return channel;
        try {
            channel = new BroadcastChannel(`drn.notifications:${currentContext.app}`);
            channel.onmessage = event => {
                if (event.data?.type === 'account' && event.data.owner !== currentContext?.owner) {
                    DRN.Notifications.suspend();
                }
            };
        } catch { channel = null; }
        return channel;
    }

    function showFailure(detail: ResponseDetail, title?: string, message?: string) {
        const xhr = detail?.xhr;
        if (!xhr || handled.has(xhr) || stale(detail)) return;
        handled.add(xhr);
        if (!affectsUserAction(detail)) return;
        if (title) {
            DRN.ErrorHandler.reportActionError(new Error(title), { ...requestContext(detail), message });
            return;
        }
        const problem = DRN.Problem.fromXhr(xhr);
        DRN.Problem.show(problem, requestContext(detail));
    }

    function refreshState() {
        configure();
        syncHistory();
        if (currentContext) importStatusMessages(document, DRN.Notifications);
        ensureResources();
    }

    function refresh() {
        refreshState();
        panel.refresh();
    }

    function start() {
        refreshState();
        panel.install();
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
    else start();
    document.addEventListener('htmx:load', refresh);
    window.addEventListener('pageshow', refresh);
    DRN.Notifications.subscribe(() => {
        const active = DRN.Notifications.isActive();
        const becameInactive = observedActive && !active;
        observedActive = active;
        if (becameInactive && currentContext) {
            epoch++;
            contextChanged();
            showAccountChange();
        }
    });
    window.addEventListener('storage', event => {
        if (currentContext && (event.key === null || event.key === `drn.notifications.v1:${currentContext.app}`))
            syncHistory();
    });
    window.addEventListener('pagehide', () => {
        channel?.close();
        channel = null;
        if (expiryTimer !== null) window.clearInterval(expiryTimer);
        expiryTimer = null;
    });

    document.addEventListener('htmx:beforeOnLoad', event => {
        // HTMX processes navigation and trigger headers before beforeSwap.
        if (stale(event.detail)) {
            loadingDetail = null;
            event.preventDefault();
            return;
        }
        loadingDetail = event.detail;
        queueMicrotask(() => { loadingDetail = null; });
    });
    document.addEventListener('htmx:beforeRequest', event => {
        if (!DRN.Notifications.isActive()) {
            event.preventDefault();
            return;
        }
        if (event.detail?.xhr) requestEpochs.set(event.detail.xhr, epoch);
    });

    document.addEventListener('htmx:beforeSwap', event => {
        const detail = event.detail;
        const xhr = detail?.xhr;
        if (!xhr) return;
        if (stale(detail)) { detail.shouldSwap = false; detail.isError = false; return; }
        const contentType = mediaType(xhr.getResponseHeader('Content-Type'));
        if (contentType === 'application/problem+json') {
            detail.shouldSwap = false;
            detail.isError = true;
            showFailure(detail);
        } else if (isValidationFragment(xhr, contentType)) {
            // Only application-owned validation fragments opt into an error-status swap.
            detail.shouldSwap = true;
            detail.isError = false;
        }
    });
    document.addEventListener('htmx:responseError', event => showFailure(event.detail));
    document.addEventListener('htmx:sendError', event => showFailure(event.detail,
        'Connection lost', 'Check your connection. The operation may not have completed.'));
    document.addEventListener('htmx:timeout', event => showFailure(event.detail,
        'The request timed out', 'The outcome is unknown. Check the current state before trying again.'));
    document.addEventListener('htmx:afterRequest', event => {
        const detail = event.detail;
        if (shouldClearProblem(detail))
            DRN.Problem.clear(requestContext(detail));
    });
    document.addEventListener('htmx:beforeCleanupElement', event => DRN.Toast.cleanup(event.detail?.elt));
    document.addEventListener('showToast', event => {
        const detail = event.detail;
        if (!detail || !loadingDetail || stale(loadingDetail)) return;
        if (typeof detail.message !== 'string' || !detail.message.trim()) return;
        // HX-Trigger fires before beforeSwap and does not include its XHR in event detail.
        // A Problem Details body takes precedence over its toast header.
        const loadingResponse = loadingDetail.xhr;
        const contentType = mediaType(loadingResponse?.getResponseHeader('Content-Type'));
        if (contentType === 'application/problem+json') return;
        if (loadingResponse?.status >= 400) {
            if (handled.has(loadingResponse)) return;
            handled.add(loadingResponse);
            if (!affectsUserAction(loadingDetail)) return;
        }
        const context = requestContext(loadingDetail);
        DRN.Toast.show({ type: detail.type, message: detail.message, duration: detail.duration,
            record: detail.record, persist: detail.persist !== false && context.persist,
            operation: detail.operation || context.operation });
    });
}
