DRN.Onmount.register('[data-js-unhandled-browser-exception]', function (options) {
    const onClick = () => {
        throw new Error('Intentional unhandled browser exception from the HTMX test page.');
    };
    this.addEventListener('click', onClick);
    options.disposable = {dispose: () => this.removeEventListener('click', onClick)};
});
