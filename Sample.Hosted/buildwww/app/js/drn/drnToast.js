//drnToast.js
import drnUtils from './drnUtils';
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
 *   Listener registered by drnNotificationEvents.js.
 */

const MAX_VISIBLE = 3;
const DEFAULT_DURATION = 5000;

const ICON_MAP = {
    success: 'bi-check-circle-fill text-success',
    error: 'bi-exclamation-triangle-fill text-danger',
    warning: 'bi-exclamation-circle-fill text-warning',
    info: 'bi-info-circle-fill text-primary'
};

let container = null;
let activeToasts = [];

/**
 * Anchors toasts inside the content area, clear of the header and sidebars.
 * On subsequent calls, verifies the cached reference is still in the DOM (handles edge cases where the body is replaced during navigation).
 * @returns {HTMLElement}
 */
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

/**
 * Creates a Bootstrap 5 toast element.
 * @param {string} type - success | error | warning | info
 * @param {string} message - Toast message text
 * @param {number} duration - Auto-dismiss duration in ms
 * @returns {HTMLElement}
 */
function createToastElement(type, message, duration) {
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
        '<div class="toast-body text-break user-select-text" style="white-space: pre-line">' + drnUtils.escapeHtml(message) + '</div>'
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

/**
 * Shows a toast notification.
 * @param {Object} options
 * @param {string} options.type - 'success' | 'error' | 'warning' | 'info'
 * @param {string} options.message - Message text
 * @param {number} [options.duration] - Auto-dismiss duration in ms (default 5000)
 */
function show(options) {
    if (window.DRN.Notifications.isActive?.() === false) return;
    if (typeof options?.message !== 'string' || !options.message.trim()) return;

    const type = Object.hasOwn(ICON_MAP, options.type) ? options.type : 'info';
    const message = options.message.trim().slice(0, 2000);
    const duration = Number.isFinite(options.duration) ? Math.max(1000, Math.min(options.duration, 30000)) : DEFAULT_DURATION;
    // The store owns notification-field validation and discards toast-only options.
    if (options.record !== false) window.DRN.Notifications.publish({ ...options, type, message });

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

const drnToast = {
    show: show,
    cleanup(root) {
        for (const entry of [...activeToasts]) {
            if (root === entry.el || root?.contains(entry.el)) {
                entry.bsToast.dispose();
                entry.el.remove();
                activeToasts.splice(activeToasts.indexOf(entry), 1);
            }
        }
    },
    success: function (message, duration) { show({ type: 'success', message: message, duration: duration }); },
    error: function (message, duration) { show({ type: 'error', message: message, duration: duration }); },
    warning: function (message, duration) { show({ type: 'warning', message: message, duration: duration }); },
    info: function (message, duration) { show({ type: 'info', message: message, duration: duration }); }
};

export default drnToast;
