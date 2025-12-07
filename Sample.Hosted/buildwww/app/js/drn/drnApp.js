/**
 * Core application object for managing state
 */
const drnApp = {
    Environment: 'Neitherland',
    IsDev: false,
    ShowCookieBanner: false,
    CsrfToken: '',
    DefaultCulture: 'tr',
    SupportedCultures: ['en', 'tr'],
    // Placeholder for application state management
    State: {}
};

// Export for potential use by other modules if needed via imports
export default drnApp;