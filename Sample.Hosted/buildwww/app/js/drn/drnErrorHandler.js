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

const MAX_ERRORS_PER_WINDOW = 5;
const RATE_WINDOW_MS = 60000;
const DEDUP_WINDOW_MS = 10000;
const REPORT_ENDPOINT = '/Api/Sample/ClientError/Report';

let errorCount = 0;
let windowStart = Date.now();
const recentErrors = new Map();

/**
 * Checks if the error should be reported (rate limit + dedup).
 * @param {string} key - Deduplication key
 * @returns {boolean}
 */
function shouldReport(key) {
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
    const json = JSON.stringify(payload);

    // Prefer sendBeacon for non-blocking fire-and-forget
    if (typeof navigator.sendBeacon === 'function') {
        const blob = new Blob([json], { type: 'application/json' });
        const sent = navigator.sendBeacon(REPORT_ENDPOINT, blob);
        if (sent) return;
    }

    // Fetch fallback
    try {
        fetch(REPORT_ENDPOINT, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: json,
            keepalive: true
        }).catch(function () { /* silently ignore reporting failures */ });
    } catch (_) {
        /* silently ignore */
    }
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
        var message = event.message || '';
        var source = event.filename || '';
        var line = event.lineno || 0;
        var column = event.colno || 0;
        var stack = event.error && event.error.stack ? event.error.stack : '';
        var key = message + '|' + source + '|' + line;

        if (shouldReport(key)) {
            sendReport(buildPayload(message, source, line, column, stack));
        }
    });

    window.addEventListener('unhandledrejection', function (event) {
        var reason = event.reason;
        var message = reason instanceof Error ? reason.message : String(reason || 'Unhandled Promise Rejection');
        var stack = reason instanceof Error ? reason.stack : '';
        var key = 'promise|' + message;

        if (shouldReport(key)) {
            sendReport(buildPayload(message, 'unhandledrejection', 0, 0, stack));
        }
    });
}

const drnErrorHandler = {
    install: install
};

export default drnErrorHandler;
