import './behaviors/home/hello-react-island';

// HTMX revisits are handled by the idempotent Onmount registrations.
if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
