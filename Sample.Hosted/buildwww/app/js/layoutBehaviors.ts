// Shared layout behaviors use runtime globals from the preceding bundles.
import './behaviors/layout/drn-footer';
import './behaviors/layout/tooltip';

// Poll after this entry's deferred registrations on the initial page.
if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
