/* The reference directive below automatically adds types for:
  - Importing .css, .module.css
  - Importing images (.png, .jpg, .svg)
  - import.meta.env (Vite environment variables)
*/
/// <reference types="vite/client" />

import type { ValidationService } from 'aspnet-client-validation';
import type { BundledBootstrap } from '@lib/bootstrap/bootstrapBundle';
import type { IDrnApp } from './drnApp.types';
import type { IDrnReact } from './drnReact.types';
import type { IDrnOnmount, OnMountStatic } from './drnOnmount.types';
import type { IDrnUtils } from './drnUtils.types';
import type { IDrnCookieManager } from './drnCookieManager.types';
import type { IDrnProblem } from './drnProblem.types';
import type { IDrnErrorHandler } from './drnErrorHandler.types';
import type { IDrnDiagnostics } from './drnDiagnostics.types';
import type { IDrnNotifications, IDrnToast } from './drnNotifications.types';

// The Main Global Augmentation
declare global {
    var DRN: IDRN;
    var onmount: OnMountStatic;
    var bootstrap: Pick<typeof import('bootstrap'), keyof BundledBootstrap>;
    interface Window {
        DRN: IDRN;
        htmx: typeof import('htmx.org').default;
        __webpack_nonce__?: string;
        CSSStyleSheet?: { new (): CSSStyleSheet; prototype: CSSStyleSheet };
        // Other globals (e.g. Google Analytics)
        //dataLayer?: any[];
    }

    // Define the shape of the global namespace object
    interface IDRN {
        App: IDrnApp;
        Onmount: IDrnOnmount;
        Utils: IDrnUtils;
        Cookie: IDrnCookieManager;
        React: IDrnReact;
        Toast: IDrnToast;
        Problem: IDrnProblem;
        Notifications: IDrnNotifications;
        ErrorHandler: IDrnErrorHandler;
        Diagnostics: IDrnDiagnostics;
        Validation?: ValidationService;
    }
}

// This export is required to ensure this file is treated as a module
// containing global augmentations.
export {};
