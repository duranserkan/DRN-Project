interface Window {
    DRN: DRN;
}

interface DRN {
    App: DrnApp;
    Onmount: DrnOnmount;
    Utils: DrnUtils;
}

interface DrnApp {
    environment: string;
    isDev: boolean;
    showCookieBanner: boolean;
    state: {
        // Define state structure if needed, e.g.:
        // components?: Record<string, any>;
        // uiFlags?: Record<string, boolean>;
        [key: string]: any; // Or more specific types
    };
    // Add other top-level properties/methods of drnApp here as needed
}