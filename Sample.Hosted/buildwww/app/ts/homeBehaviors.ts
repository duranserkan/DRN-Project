import './behaviors/home/hello-react-island.ts';

// HTMX revisits are handled by the idempotent Onmount registrations.
if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
