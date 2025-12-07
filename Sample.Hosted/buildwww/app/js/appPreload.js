// This bundle is intended to be loaded *before* jQuery and Onmount.
// It sets up core utilities and app state that might be needed later,
// but doesn't directly use jQuery/onmount itself. Check _LayoutBase for the execution order
//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean

import drnApp from './drn/drnApp';
import drnOnmount from './drn/drnOnmount';
import drnUtils from './drn/drnUtils';
import drnCookieManager from './drn/drnCookieManager';

if (typeof window !== 'undefined') {
    window.DRN = window.DRN || {};
    window.DRN.App = drnApp;
    window.DRN.Onmount = drnOnmount;
    window.DRN.Utils = drnUtils;
    window.DRN.Cookie = drnCookieManager;

    // Handle back/forward navigation by forcing a fresh request
    document.addEventListener('popstate', () => {
        window.location.href = window.location.href;
    });
}