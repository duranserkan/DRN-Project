if (typeof $ !== 'undefined' && typeof $.onmount === 'function') {
    window.onmount = $.onmount;
    onmount();
    console.log('Assigned $.onmount to global onmount');
} else {
    console.warn('$.onmount is not available.');
}

// Initialize onmount.js globally
document.addEventListener('DOMContentLoaded', function () {
    onmount();
    console.log("htmx:afterSwap'");
});

// Reinitialize onmount after HTMX partial updates
document.body.addEventListener('htmx:afterSwap', () => {
    onmount();
    console.log("htmx:afterSwap'");
});

drnApp.onmount.register('[data-bs-toggle="tooltip"]',function (options) {
    options.disposable = new bootstrap.Tooltip(this); // Initialize Bootstrap Tooltip for the current element
})