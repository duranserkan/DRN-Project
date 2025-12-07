import type {IDrnOnmount} from "@types/DrnOnmount";
import type {IDrnUtils} from "@types/DrnUtils";
import type {IDrnCookieManager} from "@types/DrnCookieManager";

declare global {
    interface Window {
        DRN: IDRN;
    }
}

interface IDRN {
    App: IDrnApp;
    Onmount: IDrnOnmount;
    Utils: IDrnUtils;
    Cookie: IDrnCookieManager;
}

interface IDrnApp {
    Environment: string;
    IsDev: boolean;
    ShowCookieBanner: boolean;
    CsrfToken: string;
    DefaultCulture: string,
    SupportedCultures: [string],
    State: {
        // Define state structure if needed, e.g.:
        // components?: Record<string, any>;
        // uiFlags?: Record<string, boolean>;
        [key: string]: any; // Or more specific types
    };
    // Add other top-level properties/methods of drnApp here as needed
}