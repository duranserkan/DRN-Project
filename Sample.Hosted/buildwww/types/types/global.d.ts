//global.d.ts

/* The reference directive below automatically adds types for:
  - Importing .css, .module.css
  - Importing images (.png, .jpg, .svg)
  - import.meta.env (Vite environment variables)
*/
/// <reference types="vite/client" />

import type {IDrnApp} from './drnApp';
import type {IDrnOnmount} from './DrnOnmount';
import type {IDrnUtils} from './DrnUtils';
import type {IDrnCookieManager} from './DrnCookieManager';

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
    // Toast, ErrorHandler, and Validation are assigned at runtime
    // (appPreload.js and htmxValidation.js). Add dedicated .d.ts
    // interfaces when their public API stabilizes.
    interface IDRN {
        App: IDrnApp;
        Onmount: IDrnOnmount;
        Utils: IDrnUtils;
        Cookie: IDrnCookieManager;
        Toast: any;
        ErrorHandler: any;
        Validation: any;
    }
}

// This export is required to ensure this file is treated as a module
// containing global augmentations.
export {};
