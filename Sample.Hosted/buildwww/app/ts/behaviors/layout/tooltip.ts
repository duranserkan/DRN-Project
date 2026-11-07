DRN.Onmount.register('[data-bs-toggle="tooltip"]', function (options) {
    options.disposable = new bootstrap.Tooltip(this, { animation: false });
});
