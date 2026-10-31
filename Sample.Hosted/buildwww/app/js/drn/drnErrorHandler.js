//drnErrorHandler.js
/**
 * Global client-side error handler.
 *
 * Captures unhandled errors and promise rejections, then reports them
 * to the server via navigator.sendBeacon (non-blocking) or fetch fallback.
 *
 * Rate-limited: max 5 errors per 60 seconds.
 * Deduplication: same message+source within 10 seconds suppressed.
 */

import drnApp from './drnApp.js';

const MAX_ERRORS_PER_WINDOW = 5;
const RATE_WINDOW_MS = 60000;
const DEDUP_WINDOW_MS = 10000;
const REPORT_ENDPOINT = '~/Api/Sample/ClientError/Report';

let errorCount = 0;
let windowStart = Date.now();
const recentErrors = new Map();

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
    const json = JSON.stringify(payload);
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
    if (typeof context.operation !== 'string' || !context.operation.trim() ||
        window.DRN.Notifications.isActive() === false) return null;

    const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
    const clientErrorId = Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('');
    const message = (error instanceof Error ? error.message : String(error || 'Action failed')) || 'Action failed';
    const payload = buildPayload(message, 'user-action', 0, 0, error instanceof Error ? error.stack : '');
    payload.clientErrorId = clientErrorId;
    // Each action occurrence needs its own reference. Keep the shared rate limit.
    if (tryReserveReportSlot(clientErrorId)) sendReport(payload);

    const publicMessage = typeof context.message === 'string' && context.message.trim()
        ? context.message : 'The action could not be completed. Check the current state before trying again.';
    const record = window.DRN.Notifications.publish({
        type: 'error', title: 'Action could not be completed', message: publicMessage,
        clientErrorId, operation: context.operation, persist: context.persist !== false
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

/**
 * Installs global error handlers.
 * Uses addEventListener so multiple handlers can coexist without silent overwrites.
 */
function install() {
    window.addEventListener('error', function (event) {
        const message = event.message || '';
        const source = event.filename || '';
        const line = event.lineno || 0;
        const column = event.colno || 0;
        const stack = event.error?.stack || '';
        const key = message + '|' + source + '|' + line;

        if (tryReserveReportSlot(key)) {
            sendReport(buildPayload(message, source, line, column, stack));
        }
    });

    window.addEventListener('unhandledrejection', function (event) {
        const reason = event.reason;
        const message = reason instanceof Error ? reason.message : String(reason || 'Unhandled Promise Rejection');
        const stack = reason instanceof Error ? reason.stack : '';
        const key = 'promise|' + message;

        if (tryReserveReportSlot(key)) {
            sendReport(buildPayload(message, 'unhandledrejection', 0, 0, stack));
        }
    });
}

const drnErrorHandler = {
    install: install,
    reportActionError
};

export default drnErrorHandler;
