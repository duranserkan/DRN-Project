// Initialize onmount.js globally
window.addEventListener('popstate', () => {
    // History changed (back/forward navigation)
    window.location.href = window.location.href; // Forces a fresh request
    console.log("popstate");
});
document.addEventListener('DOMContentLoaded', () => {
    onmount();
    console.log("DOMContentLoaded");
});

// Reinitialize onmount after HTMX partial updates
document.body.addEventListener('htmx:afterSwap', () => {
    onmount();
});
onmount('[data-bs-toggle="tooltip"]', function (options) {
    // Initialize Bootstrap Tooltip for the current element
    options.Tooltip = new bootstrap.Tooltip(this);
}, function (options) {
    // Dispose Bootstrap Tooltip for the current element
    if (!options.Tooltip) return;
    options.Tooltip.dispose();
},); 

