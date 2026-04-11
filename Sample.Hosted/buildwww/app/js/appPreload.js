//appPreload.js
// This bundle is intended to be loaded early — before Onmount and library bundles.
// It sets up core utilities and app state that might be needed later.
// Check _LayoutBase for the execution order
//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean

import drnErrorHandler from './drn/drnErrorHandler';
import drnApp from './drn/drnApp';
import drnOnmount from './drn/drnOnmount';
import drnUtils from './drn/drnUtils';
import drnCookieManager from './drn/drnCookieManager';
import drnToast from './drn/drnToast';

if (typeof window !== 'undefined') {
    window.DRN = window.DRN || {};
    window.DRN.App = drnApp;
    window.DRN.Onmount = drnOnmount;
    window.DRN.Utils = drnUtils;
    window.DRN.Cookie = drnCookieManager;
    window.DRN.Toast = drnToast;
    window.DRN.ErrorHandler = drnErrorHandler;
    window.DRN.React = window.DRN.React || {}; //react will be mounted lazily in reactBundle.tsx

    // Install global error handler as early as possible
    drnErrorHandler.install();

    // Handle back/forward navigation by forcing a fresh request
    document.addEventListener('popstate', () => {
        window.location.href = window.location.href;
    });
}