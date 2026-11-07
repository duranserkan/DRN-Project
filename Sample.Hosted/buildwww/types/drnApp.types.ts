export interface IDrnApp {
    Environment: string;
    IsDev: boolean;
    ShowCookieBanner: boolean;
    CsrfToken: string;
    DefaultCulture: string;
    SupportedCultures: string[];
    /** Resolves ~/ paths under the server-provided application base. Other URLs are unchanged. */
    url(path: string): string;
    State: Record<string, unknown>;
}
