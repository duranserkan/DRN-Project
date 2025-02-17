//Jquery && Onmount.js is not available yet. Check _LayoutBase for the execution order
//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean

//Manages the app and provides utility methods
window.drnApp = {
    environment: 'Neitherland',
    isDev: false,
    // provides utility methods
    utils: {},
    // manages behavior of elements.
    onmount: {}
};
document.addEventListener('popstate', () => { // History changed (back/forward navigation)
    window.location.href = window.location.href; // Forces a fresh request
});

//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean
/**
 * @param {string} str
 */
drnApp.utils.urlSafeBase64Encode = str => {
    // Base64 encode the string
    const base64 = btoa(str);

    // Convert to URL-safe Base64
    // Remove padding '=' characters
    return base64
        .replace(/\+/g, '-')  // Replace '+' with '-'
        .replace(/\//g, '_')  // Replace '/' with '_'
        .replace(/=+$/, '');
}

/**
 * @param {string} str
 */
drnApp.utils.urlSafeBase64Decode = str => {
    // Replace URL-safe characters back to standard Base64 characters
    const base64 = str
        .replace(/-/g, '+')  // Replace '-' with '+'
        .replace(/_/g, '/')  // Replace '_' with '/'
        .padEnd(str.length + (4 - str.length % 4) % 4, '=');  // Add padding if necessary

    // Decode Base64 string
    return atob(base64);
}

/**
 * @param {string} cookieName
 */
drnApp.utils.checkCookieExists = cookieName => document.cookie.split('; ').some(cookie => cookie.startsWith(`${cookieName}=`))

/**
 * @param {Element} requestElement
 */
drnApp.utils.getRequestElementSelector = requestElement => {
    if (!requestElement) return 'Unknown Element';

    let selectorSuffix = '';
    if (requestElement.id) selectorSuffix += `#${requestElement.id}`;
    if (requestElement.classList.length > 0) selectorSuffix += `.${[...requestElement.classList].join('.')}`;

    return requestElement.tagName + selectorSuffix;
};

//https://ricostacruz.com/rsjs/index.html#consider-using-onmount
drnApp.onmount.unregister = function (options) {
    if (!this) return;

    $(this).off();
    if (options.disposable) {
        options.disposable.dispose();
    }
}

/**
 * Registers the given selector with onmount and ensures that registration
 * occurs once the DOM is ready or onmount.js is loaded.
 *
 * @param {string} selector - CSS selector for elements to be mounted.
 * @param {Function} registerCallback - Callback to execute upon registration.
 */
drnApp.onmount.register = function (selector, registerCallback) {
    drnApp.onmount.registerFull(selector, registerCallback, drnApp.onmount.unregister);
}

/**
 * Registers the given selector with onmount and ensures that registration
 * occurs once the DOM is ready or onmount.js is loaded.
 *
 * @param {string} selector - CSS selector for elements to be mounted.
 * @param {Function} registerCallback - Callback to execute upon registration.
 * @param {Function} [unregisterCallback] - Optional callback to execute if registration fails.
 */
drnApp.onmount.registerFull = (selector, registerCallback, unregisterCallback) => {
    const POLLING_INTERVAL = 100; // in milliseconds
    const MAX_POLL_ATTEMPTS = 50; // 5 seconds total

    if (typeof selector !== 'string' || selector.trim() === '') {
        console.error('Invalid selector provided');
        return;
    }
    if (typeof registerCallback !== 'function') {
        console.error('Registration callback must be a function');
        return;
    }

    const executeRegistration = () => onmount(selector, registerCallback, unregisterCallback);
    const handleDocumentReady = () => {
        if (typeof onmount !== 'function')
            startPolling();
        else
            executeRegistration();
    };

    const startPolling = () => {
        let attempts = 0;
        const poll = setInterval(() => {
            if (typeof onmount === 'function') {
                clearInterval(poll);
                executeRegistration();
            } else if (++attempts >= MAX_POLL_ATTEMPTS) {
                clearInterval(poll);
                console.error(`Failed to load onmount.js after ${MAX_POLL_ATTEMPTS * POLLING_INTERVAL}ms`);
            }
        }, POLLING_INTERVAL);
    };

    const checkDocumentState = () => {
        if (document.readyState === 'complete' || document.readyState === 'interactive')
            handleDocumentReady();
        else
            document.addEventListener('DOMContentLoaded', handleDocumentReady, {once: true});
    };

    checkDocumentState();
};