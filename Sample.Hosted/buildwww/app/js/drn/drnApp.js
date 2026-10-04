//drnApp.js
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
    // Only ~/ denotes an application URL. Root-relative and resolved URLs stay unchanged.
    url(path) {
        if (!path.startsWith('~/')) return path;
        const base = document.querySelector('meta[name="drn-app-base"]')?.content || '/';
        return base.replace(/\/$/, '') + path.substring(1);
    },
    // Placeholder for application state management
    State: {}
};

// Export for potential use by other modules if needed via imports
export default drnApp;
