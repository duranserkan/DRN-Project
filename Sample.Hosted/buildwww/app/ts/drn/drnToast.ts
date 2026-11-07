import drnUtils from './drnUtils.ts';
/**
 * Centralized toast notification service.
 *
 * API:
 *   DRN.Toast.show({ type, message, duration? })
 *   DRN.Toast.success(message)
 *   DRN.Toast.error(message)
 *   DRN.Toast.warning(message)
 *   DRN.Toast.info(message)
 *
 * HTMX integration:
 *   Server sends HX-Trigger: showToast header with JSON payload.
 *   Listener registered by drnNotificationEvents.ts.
 */

import type { IDrnToast, NotificationType } from '@/types/drnNotifications.types';

const MAX_VISIBLE = 3;
const DEFAULT_DURATION = 5000;
const MAX_TOASTS_PER_WINDOW = 5;
const TOAST_WINDOW_MS = 10000;

const ICON_MAP = {
    success: 'bi-check-circle-fill text-success',
    error: 'bi-exclamation-triangle-fill text-danger',
    warning: 'bi-exclamation-circle-fill text-warning',
    info: 'bi-info-circle-fill text-primary',
};

let container: HTMLDivElement | null = null;
const activeToasts: { el: HTMLDivElement; bsToast: InstanceType<typeof bootstrap.Toast> }[] = [];
let presentedAt: number[] = [];

/** Reuses the toast container while it remains attached after navigation. */
function getContainer() {
    if (container && document.body.contains(container)) return container;

    container = document.createElement('div');
    const host = document.querySelector('[data-toast-host]');
    container.className = host
        ? 'toast-container position-absolute top-0 end-0 p-3 overflow-auto mh-100'
        : 'toast-container position-fixed bottom-0 start-50 translate-middle-x p-3 overflow-auto mh-100';
    // Keep content feedback below Bootstrap offcanvas backdrops (1040) and modal dialogs.
    container.style.zIndex = '1030';
    container.setAttribute('aria-label', 'Toast notifications');
    (host || document.body).appendChild(container);
    return container;
}

function createToastElement(type: NotificationType, message: string, duration: number) {
    const validType = ICON_MAP[type] ? type : 'info';
    const iconClass = ICON_MAP[validType];
    const title = drnUtils.escapeHtml(validType.charAt(0).toUpperCase() + validType.slice(1));

    const el = document.createElement('div');
    el.className = 'toast bg-body';
    // The shared shell owns the pre-existing live region, avoiding duplicate announcements.
    el.dataset.bsDelay = String(duration);

    el.innerHTML = [
        '<div class="toast-header bg-body">',
        '  <i class="bi ' + iconClass + ' me-2"></i>',
        '  <strong class="me-auto">' + title + '</strong>',
        '  <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>',
        '</div>',
        '<div class="toast-body text-break user-select-text" style="white-space: pre-line">' +
            drnUtils.escapeHtml(message) +
            '</div>',
    ].join('\n');

    return el;
}

/**
 * Enforces the maximum visible toast limit (FIFO).
 */
function enforceLimit() {
    while (activeToasts.length >= MAX_VISIBLE) {
        const oldest = activeToasts.shift();
        oldest?.bsToast?.hide();
    }
}

/** Uses a five-second display duration by default. */
function show(options: Parameters<IDrnToast['show']>[0]) {
    if (window.DRN.Notifications.isActive?.() === false) return;
    if (typeof options?.message !== 'string' || !options.message.trim()) return;

    const type = options.type && Object.hasOwn(ICON_MAP, options.type) ? options.type : 'info';
    const message = options.message.trim().slice(0, 2000);
    const duration =
        typeof options.duration === 'number' && Number.isFinite(options.duration)
            ? Math.max(1000, Math.min(options.duration, 30000))
            : DEFAULT_DURATION;
    // The store owns notification-field validation and discards toast-only options.
    if (options.record !== false) window.DRN.Notifications.publish({ ...options, type, message });

    // Retain history, but do not queue a burst for later playback.
    const now = Date.now();
    presentedAt = presentedAt.filter((time) => now - time < TOAST_WINDOW_MS);
    if (presentedAt.length >= MAX_TOASTS_PER_WINDOW) return;
    presentedAt.push(now);

    const live = document.getElementById(type === 'error' ? 'drn-notification-assertive' : 'drn-notification-polite');
    if (live) live.textContent = message;

    enforceLimit();

    const toastEl = createToastElement(type, message, duration);
    getContainer().appendChild(toastEl);

    // Immediate transitions make disposal safe when HTMX replaces the containing body.
    const bsToast = new bootstrap.Toast(toastEl, { animation: false });
    const entry = { el: toastEl, bsToast: bsToast };
    activeToasts.push(entry);

    toastEl.addEventListener('hidden.bs.toast', function () {
        const idx = activeToasts.indexOf(entry);
        if (idx > -1) activeToasts.splice(idx, 1);
        bsToast.dispose();
        toastEl.remove();
    });

    bsToast.show();
}

const drnToast: IDrnToast = {
    show: show,
    cleanup(root) {
        // Iterate backwards because removing a toast mutates the active collection.
        for (let index = activeToasts.length - 1; index >= 0; index--) {
            const entry = activeToasts[index]!;
            if (root === entry.el || root?.contains(entry.el)) {
                entry.bsToast.dispose();
                entry.el.remove();
                activeToasts.splice(index, 1);
            }
        }
    },
    success: function (message, duration = DEFAULT_DURATION) {
        show({ type: 'success', message: message, duration: duration });
    },
    error: function (message, duration = DEFAULT_DURATION) {
        show({ type: 'error', message: message, duration: duration });
    },
    warning: function (message, duration = DEFAULT_DURATION) {
        show({ type: 'warning', message: message, duration: duration });
    },
    info: function (message, duration = DEFAULT_DURATION) {
        show({ type: 'info', message: message, duration: duration });
    },
};

export default drnToast;
