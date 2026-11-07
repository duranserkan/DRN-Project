import type { IDrnApp } from '@/types/drnApp.types';

/**
 * Core application object for managing state
 */
const drnApp: IDrnApp = {
    Environment: 'Neitherland',
    IsDev: false,
    ShowCookieBanner: false,
    CsrfToken: '',
    DefaultCulture: 'tr',
    SupportedCultures: ['en', 'tr'],
    // Only ~/ denotes an application URL. Root-relative and resolved URLs stay unchanged.
    url(path: string) {
        if (!path.startsWith('~/')) return path;
        const base = document.querySelector<HTMLMetaElement>('meta[name="drn-app-base"]')?.content || '/';
        return base.replace(/\/$/, '') + path.substring(1);
    },
    // Placeholder for application state management
    State: {}
};

// Export for potential use by other modules if needed via imports
export default drnApp;
