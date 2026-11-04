import './behaviors/test/unhandled-browser-exception';

if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', () => window.onmount(), {once: true});
