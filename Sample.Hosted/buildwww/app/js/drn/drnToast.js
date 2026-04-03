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
 *   Listener registered in appPostload.js.
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
 * Lazily creates the fixed-position toast container in the DOM.
 * On subsequent calls, verifies the cached reference is still in the DOM (handles edge cases where the body is replaced during navigation).
 * @returns {HTMLElement}
 */
function getContainer() {
    if (container && document.body.contains(container)) return container;

    container = document.createElement('div');
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '1090'; //places toasts above Bootstrap modals (1055) but below popovers (1095).
    container.setAttribute('aria-label', 'Notifications');
    document.body.appendChild(container);
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
    el.className = 'toast';
    el.setAttribute('role', 'alert');
    el.setAttribute('aria-live', 'assertive');
    el.setAttribute('aria-atomic', 'true');
    el.setAttribute('data-bs-delay', String(duration));

    el.innerHTML = [
        '<div class="toast-header">',
        '  <i class="bi ' + iconClass + ' me-2"></i>',
        '  <strong class="me-auto">' + title + '</strong>',
        '  <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>',
        '</div>',
        '<div class="toast-body">' + drnUtils.escapeHtml(message) + '</div>'
    ].join('\n');

    return el;
}

/**
 * Enforces the maximum visible toast limit (FIFO).
 */
function enforceLimit() {
    while (activeToasts.length >= MAX_VISIBLE) {
        const oldest = activeToasts.shift();
        if (oldest && oldest.bsToast) {
            oldest.bsToast.hide();
        }
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
    if (!options || !options.message) return;

    const type = options.type || 'info';
    const message = options.message;
    const duration = options.duration || DEFAULT_DURATION;

    enforceLimit();

    const toastEl = createToastElement(type, message, duration);
    getContainer().appendChild(toastEl);

    const bsToast = new bootstrap.Toast(toastEl);
    const entry = { el: toastEl, bsToast: bsToast };
    activeToasts.push(entry);

    toastEl.addEventListener('hidden.bs.toast', function () {
        const idx = activeToasts.indexOf(entry);
        if (idx > -1) activeToasts.splice(idx, 1);
        toastEl.remove();
    });

    bsToast.show();
}

const drnToast = {
    show: show,
    success: function (message, duration) { show({ type: 'success', message: message, duration: duration }); },
    error: function (message, duration) { show({ type: 'error', message: message, duration: duration }); },
    warning: function (message, duration) { show({ type: 'warning', message: message, duration: duration }); },
    info: function (message, duration) { show({ type: 'info', message: message, duration: duration }); }
};

export default drnToast;
