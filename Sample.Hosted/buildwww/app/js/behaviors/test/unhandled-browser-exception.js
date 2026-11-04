DRN.Onmount.register('[data-js-unhandled-browser-exception]', function (options) {
    const button = this;
    const onClick = () => {
        throw new Error('Intentional unhandled browser exception from the HTMX test page.');
    };
    button.addEventListener('click', onClick);
    options.disposable = {dispose: () => button.removeEventListener('click', onClick)};
});
