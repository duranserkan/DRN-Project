import './behaviors/test/unhandled-browser-exception.ts';

if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
