// Shared layout behaviors use runtime globals from the preceding bundles.
import './behaviors/layout/drn-footer.ts';
import './behaviors/layout/tooltip.ts';

// Poll after this entry's deferred registrations on the initial page.
if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
