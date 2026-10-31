//drnErrorHandler.js
/**
 * Global client-side error handler.
 *
 * Captures unhandled errors and promise rejections, then reports them
 * to the server via navigator.sendBeacon (non-blocking) or fetch fallback.
 * Adds safe notification history entries with the same client error reference.
 *
 * Rate-limited: max 5 errors per 60 seconds.
 * Deduplication: same message+source within 10 seconds suppressed.
 */

import drnApp from './drnApp.js';

const MAX_ERRORS_PER_WINDOW = 5;
const RATE_WINDOW_MS = 60000;
const DEDUP_WINDOW_MS = 10000;
const REPORT_ENDPOINT = '~/Api/Sample/ClientError/Report';
// Must match ClientErrorController's RequestSizeLimit, including JSON overhead.
const MAX_REPORT_BYTES = 3600;

function serializeReport(payload) {
    const encoder = new TextEncoder();
    const report = { ...payload };
    let json = JSON.stringify(report);
    // Preserve the correlation ID and trim less useful diagnostics first.
    for (const field of ['stack', 'userAgent', 'url', 'source', 'message']) {
        if (encoder.encode(json).byteLength <= MAX_REPORT_BYTES) return json;
        const characters = Array.from(report[field]);
        let low = field === 'message' ? 1 : 0;
        let high = characters.length;
        while (low < high) {
            const middle = Math.ceil((low + high) / 2);
            report[field] = characters.slice(0, middle).join('');
            if (encoder.encode(JSON.stringify(report)).byteLength <= MAX_REPORT_BYTES) low = middle;
            else high = middle - 1;
        }
        report[field] = characters.slice(0, low).join('');
        json = JSON.stringify(report);
    }
    return encoder.encode(json).byteLength <= MAX_REPORT_BYTES ? json : null;
}

let errorCount = 0;
let windowStart = Date.now();
const recentErrors = new Map();
let handlingUnhandledError = false;

function createClientErrorId() {
    const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
    return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
}

/**
 * Reserves a report slot if rate limiting and deduplication allow it.
 * @param {string} key - Deduplication key
 * @returns {boolean}
 */
function tryReserveReportSlot(key) {
    const now = Date.now();

    // Reset rate window
    if (now - windowStart > RATE_WINDOW_MS) {
        errorCount = 0;
        windowStart = now;
    }

    // Rate limit
    if (errorCount >= MAX_ERRORS_PER_WINDOW) return false;

    // Dedup
    const lastSeen = recentErrors.get(key);
    if (lastSeen && (now - lastSeen) < DEDUP_WINDOW_MS) return false;

    recentErrors.set(key, now);
    errorCount++;

    // Prune old dedup entries
    if (recentErrors.size > 50) {
        for (const [k, v] of recentErrors) {
            if (now - v > DEDUP_WINDOW_MS) recentErrors.delete(k);
        }
    }

    return true;
}

/**
 * Sends the error payload to the server.
 * @param {Object} payload
 */
function sendReport(payload) {
    try {
        dispatchReport(payload);
    } catch { /* Reporting must not interrupt the failed action's feedback. */ }
}

function dispatchReport(payload) {
    const owner = document.querySelector('[data-notification-context]')?.dataset.notificationOwner;
    if (!owner || owner === 'anonymous' || window.DRN?.Notifications?.isActive?.() === false) return;
    const json = serializeReport(payload);
    if (json === null) return;
    const endpoint = drnApp.url(REPORT_ENDPOINT);

    // Prefer sendBeacon for non-blocking fire-and-forget
    if (typeof navigator.sendBeacon === 'function') {
        const blob = new Blob([json], { type: 'application/json' });
        const sent = navigator.sendBeacon(endpoint, blob);
        if (sent) return;
    }

    // Fetch fallback
    fetch(endpoint, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: json,
        keepalive: true
    }).catch(function () { /* silently ignore reporting failures */ });
}

/** Explicit action boundary. Global errors cannot reliably identify user intent. */
function reportActionError(error, context = {}) {
    try {
        return reportActionErrorCore(error, context);
    } catch {
        // Feedback must not replace the original action failure.
        return null;
    }
}

function reportActionErrorCore(error, context) {
    const notifications = window.DRN?.Notifications;
    if (typeof context.operation !== 'string' || !context.operation.trim() ||
        notifications?.isActive?.() !== true) return null;

    const message = (error instanceof Error ? error.message : String(error || 'Action failed')) || 'Action failed';
    const payload = buildPayload(message, 'user-action', 0, 0, error instanceof Error ? error.stack : '');
    // Each action occurrence needs its own reference. Keep the shared rate limit.
    if (tryReserveReportSlot(payload.clientErrorId)) sendReport(payload);

    const publicMessage = typeof context.message === 'string' && context.message.trim()
        ? context.message : 'The action could not be completed. Check the current state before trying again.';
    const record = notifications.publish({
        type: 'error', title: 'Action could not be completed', message: publicMessage,
        clientErrorId: payload.clientErrorId, operation: context.operation, persist: context.persist !== false
    });
    if (record) window.DRN.Toast.show({ type: 'error', message: publicMessage, record: false });
    return record;
}

/**
 * Builds a normalized error payload.
 * @param {string} message
 * @param {string} source
 * @param {number} line
 * @param {number} column
 * @param {string} stack
 * @returns {Object}
 */
function buildPayload(message, source, line, column, stack) {
    return {
        clientErrorId: createClientErrorId(),
        message: String(message || '').substring(0, 500),
        source: String(source || '').substring(0, 200),
        line: line || 0,
        column: column || 0,
        stack: String(stack || '').substring(0, 2000),
        url: window.location.href.substring(0, 500),
        userAgent: navigator.userAgent.substring(0, 300),
        timestamp: new Date().toISOString()
    };
}

function reportUnhandledError(event, isRejection) {
    if (handlingUnhandledError) return;
    handlingUnhandledError = true;
    try {
        const reason = isRejection ? event.reason : event.error;
        const message = isRejection
            ? reason instanceof Error ? reason.message : String(reason || 'Unhandled Promise Rejection')
            : event.message || '';
        const source = isRejection ? 'unhandledrejection' : event.filename || '';
        const line = isRejection ? 0 : event.lineno || 0;
        const column = isRejection ? 0 : event.colno || 0;
        const key = isRejection ? 'promise|' + message : message + '|' + source + '|' + line;
        if (!tryReserveReportSlot(key)) return;

        const payload = buildPayload(message, source, line, column, reason?.stack || '');
        sendReport(payload);

        const notifications = window.DRN?.Notifications;
        if (notifications?.isActive() !== true) return;
        const messageForUser = 'A page encountered a problem. If you were completing an action, check its result before trying again.';
        const record = notifications.publish({
            type: 'error', title: 'An unexpected error occurred',
            message: messageForUser,
            clientErrorId: payload.clientErrorId,
            operation: 'Page error'
        });
        if (record) window.DRN.Toast?.show({ type: 'error', message: messageForUser, record: false });
    } catch { /* A failing global handler must not generate another unhandled error. */ }
    finally { handlingUnhandledError = false; }
}

/** Installs global handlers without suppressing the browser's default error reporting. */
function install() {
    window.addEventListener('error', event => reportUnhandledError(event, false));
    window.addEventListener('unhandledrejection', event => reportUnhandledError(event, true));
}

const drnErrorHandler = {
    install: install,
    reportActionError
};

export default drnErrorHandler;
