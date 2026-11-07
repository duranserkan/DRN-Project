// This bundle is intended to be loaded early — before Onmount and library bundles.
// Check _LayoutBase for the execution order
//https://ricostacruz.com/rsjs/index.html#keep-the-global-namespace-clean

import drnErrorHandler from './drn/drnErrorHandler';
import drnDiagnostics from './drn/drnDiagnostics';
import drnApp from './drn/drnApp';
import drnOnmount from './drn/drnOnmount';
import drnUtils from './drn/drnUtils';
import drnCookieManager from './drn/drnCookieManager';
import drnToast from './drn/drnToast';
import drnProblem from './drn/drnProblem';
import drnNotifications from './drn/drnNotifications';

if (typeof window !== 'undefined' && !window.DRN?.ErrorHandler) {
    // Preload sets up core utilities and app state.
    const services: IDRN = {
        App: drnApp,
        Onmount: drnOnmount,
        Utils: drnUtils,
        Cookie: drnCookieManager,
        Toast: drnToast,
        Problem: drnProblem,
        Notifications: drnNotifications,
        ErrorHandler: drnErrorHandler,
        Diagnostics: drnDiagnostics,
        // DRN.React.mount returns null until the optional React bundle installs its implementation.
        React: window.DRN?.React ?? { mount: () => null }
    };
    window.DRN = Object.assign(window.DRN ?? {}, services);

    // Install global error handler as early as possible
    drnErrorHandler.install();

    // Handle back/forward navigation by forcing a fresh request
    document.addEventListener('popstate', () => {
        window.location.reload();
    });
}
