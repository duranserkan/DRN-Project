//appPostload.js
if (typeof window.onmount === 'function') {
    onmount();
} else {
    console.warn('window.onmount is not available.');
}

// Initialize onmount.js globally
document.addEventListener('DOMContentLoaded', onmount, {once: true});

// Reinitialize onmount after HTMX partial updates
document.addEventListener('htmx:load', onmount);

DRN.Onmount.register('[data-bs-toggle="tooltip"]', function (options) {
    options.disposable = new bootstrap.Tooltip(this, {animation: false}); // Initialize Bootstrap Tooltip for the current element
})

document.addEventListener('htmx:responseError', function (evt) {
    const response = evt.detail?.xhr;
    if (response) DRN.Problem.show(DRN.Problem.fromXhr(response));
});

// HTMX integration: listen for showToast event from HX-Trigger response headers
document.addEventListener('showToast', function (evt) {
    const detail = evt.detail;
    const canToast = detail && window.DRN?.Toast;
    if (!canToast)
        return;
    window.DRN.Toast.show({
        type: detail.type || 'info',
        message: detail.message || '',
        duration: detail.duration
    });
});
