//drnApp.d.ts
export interface IDrnApp {
    Environment: string;
    IsDev: boolean;
    ShowCookieBanner: boolean;
    CsrfToken: string;
    DefaultCulture: string,
    SupportedCultures: string[],
    /** Resolves ~/ paths under the server-provided application base. Other URLs are unchanged. */
    url(path: string): string;
    State: {
        // Define state structure if needed, e.g.:
        // components?: Record<string, any>;
        // uiFlags?: Record<string, boolean>;
        [key: string]: any; // Or more specific types
    };
    // Add other top-level properties/methods of drnApp here as needed
}
