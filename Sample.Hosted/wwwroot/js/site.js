//Jquery && Onmount.js is not available yet check _LayoutBase execution order
document.addEventListener('popstate', () => { // History changed (back/forward navigation)
    window.location.href = window.location.href; // Forces a fresh request
});

//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean
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

drnApp.utils.urlSafeBase64Decode = str => {
    // Replace URL-safe characters back to standard Base64 characters
    const base64 = str
        .replace(/-/g, '+')  // Replace '-' with '+'
        .replace(/_/g, '/')  // Replace '_' with '/'
        .padEnd(str.length + (4 - str.length % 4) % 4, '=');  // Add padding if necessary

    // Decode Base64 string
    return atob(base64);
}

drnApp.utils.checkCookieExists = cookieName => document.cookie.split('; ').some(cookie => cookie.startsWith(`${cookieName}=`))

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

drnApp.onmount.registerFull = function (selector, registerCallBack, unregisterCallBack) {
    function register() {
        if (typeof onmount === 'function') {
            onmount(selector, registerCallBack, unregisterCallBack);
        } else {
            console.warn('onmount.js is not available yet. Waiting for document to load...');
            document.addEventListener('DOMContentLoaded', function () {
                if (typeof onmount === 'function') {
                    onmount(selector, registerCallBack, unregisterCallBack);
                    console.log(`Registered ${selector} after document load.`);
                } else {
                    console.error('onmount.js is still not available after document load.');
                }
            });
        }
    }

    // If document is already loaded, register immediately
    if (document.readyState === 'complete' || document.readyState === 'interactive') {
        register();
    } else {    // Wait for document to load before registering
        document.addEventListener('DOMContentLoaded', register);
    }
}

drnApp.onmount.register = function (selector, registerCallBack) {
    drnApp.onmount.registerFull(selector, registerCallBack, drnApp.onmount.unregister);
}

if (drnApp.isDev) {
    document.addEventListener('htmx:responseError', function (evt) {
        if (!evt.detail) {
            console.error("htmx:responseError fired without detail");
            return;
        }

        const response = evt.detail.xhr;
        const request = evt.detail.requestConfig;
        
        if (!response || !request) {
            console.error("Missing xhr or requestConfig in htmx:responseError detail");
            return;
        }

        // Extract relevant details with fallback defaults
        const endpoint = request.path || "unknown endpoint";
        const method = request.method || 'GET';
        const requestData = request.body || 'No data sent';
        const status = response.status;
        const statusText = response.statusText;

        // Extract request headers
        const requestHeaders = request.headers || {};
        const headersString = Object.entries(requestHeaders)
            .map(([key, value]) => `${key}: ${value}`)
            .join('\n') || 'No headers sent';
        
        const requestElementSelector = drnApp.utils.getRequestElementSelector(evt.target);
        const targetSelector = drnApp.utils.getRequestElementSelector(request.target) || 'Unknown Target';

        // Construct a detailed error message
        const errorMessage = `
Request failed:
- Endpoint: ${endpoint}
- Method: ${method}
- Status: ${status} (${statusText})
- Request Element: ${requestElementSelector}
- Target Selector: ${targetSelector}
- Request Headers:
------- 
${headersString}
-------
- Data Sent:
------- 
${requestData}
-------
`;

        alert(errorMessage.trim());
    });
}