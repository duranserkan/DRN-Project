//Jquery && Onmount.js is not available yet. Check _LayoutBase for the execution order
//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean

//Manages the app and provides utility methods
window.drnApp = {
    environment: 'Neitherland',
    isDev: false,
    // provides utility methods
    utils: {},
    // manages behavior of elements.
    onmount: {},
    // to manages state of elements.
    state: {}
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

    Array.from(requestElement.attributes)
        .filter(attr => attr.name.startsWith('data-'))
        .forEach(dataAttr => {
            selectorSuffix += `[${dataAttr.name}="${dataAttr.value}"]`;
        });

    return requestElement.tagName + selectorSuffix;
};

//https://ricostacruz.com/rsjs/index.html#consider-using-onmount
//https://rstacruz.github.io/onmount
drnApp.onmount.unregister = function (options) {
    if (!this) return;

    if (options.disposable && typeof options.disposable.dispose === 'function') {
        options.disposable.dispose();
    }

    $(this).off();
}

/**
 * Registers the given selector with onmount and ensures that registration
 * occurs once the DOM is ready or onmount.js is loaded.
 *
 * @param {string} selector - CSS selector for elements to be mounted.
 * @param {Function} registerCallback - Callback to execute upon registration.
 * @param {string} [idempotencyKey] - Optional key that prevents duplicate registrations of the selector. Selector and key together determine uniquness.
 */
drnApp.onmount.register = function (selector, registerCallback, idempotencyKey = '') {
    drnApp.onmount.registerFull(selector, registerCallback, drnApp.onmount.unregister, idempotencyKey);
}

/**
 * Registers the given selector with onmount and ensures that registration
 * occurs once the DOM is ready or onmount.js is loaded.
 *
 * @param {string} selector - CSS selector for elements to be mounted.
 * @param {Function} registerCallback - Callback to execute upon registration.
 * @param {Function} [unregisterCallback] - Optional callback to execute if registration fails.
 * @param {string} [idempotencyKey] - Optional key that prevents duplicate registrations of the selector. Selector and key together determine uniquness.
 */
drnApp.onmount.registerFull = (selector, registerCallback, unregisterCallback = undefined, idempotencyKey = '') => {
    if (typeof selector !== 'string' || selector.trim() === '') {
        console.error('Invalid selector provided');
        return;
    }
    if (typeof registerCallback !== 'function') {
        console.error('Registration callback must be a function');
        return;
    }
    if (typeof unregisterCallback !== 'function') {
        unregisterCallback = drnApp.onmount.unregister;
    }
    if (typeof idempotencyKey !== 'string') {
        console.error('Registration key must be a string');
        return;
    }
    const selectorKey = selector + '_' + idempotencyKey;

    if (!drnApp.onmount._registry)
        drnApp.onmount._registry = new Set();

    if (drnApp.onmount._registry.has(selectorKey)) {
        onmount();
        return;
    }


    drnApp.onmount._registry.add(selectorKey);

    const executeRegistration = () => onmount(selector, registerCallback, unregisterCallback);

    let readyState = document.readyState;
    if (readyState === 'interactive' || readyState === 'complete')
        executeRegistration();
    else
        document.addEventListener('DOMContentLoaded', executeRegistration, {once: true});

    onmount();
};