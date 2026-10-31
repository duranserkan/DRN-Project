//global.d.ts

/* The reference directive below automatically adds types for:
  - Importing .css, .module.css
  - Importing images (.png, .jpg, .svg)
  - import.meta.env (Vite environment variables)
*/
/// <reference types="vite/client" />

import type {IDrnApp} from './drnApp';
import type {IDrnReact} from './DrnReact';
import type {IDrnOnmount} from './DrnOnmount';
import type {IDrnUtils} from './DrnUtils';
import type {IDrnCookieManager} from './DrnCookieManager';
import type {IDrnProblem} from './DrnProblem';
import type {IDrnErrorHandler} from './DrnErrorHandler';
import type {IDrnNotifications, IDrnToast} from './DrnNotifications';

// The Main Global Augmentation
declare global {
    // Extend the Window interface
    interface Window {
        DRN: IDRN;
        CSSStyleSheet?: { new(): CSSStyleSheet; prototype: CSSStyleSheet };
        // Other globals (e.g. Google Analytics)
        //dataLayer?: any[];
    }

    // Define the shape of the global namespace object
    // Validation still uses a runtime-owned interface.
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
        Validation: any;
    }
}

// This export is required to ensure this file is treated as a module
// containing global augmentations.
export {};
