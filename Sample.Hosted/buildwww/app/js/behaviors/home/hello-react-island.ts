DRN.Onmount.register('[data-js-hello-react-island]', function (options) {
    if (!(this instanceof HTMLElement)) return;
    const versions = this.dataset.versions;
    let clickCount = 0;
    let titleIndex = 0;
    let transitionTimer: ReturnType<typeof setTimeout> | undefined;
    const titles = ['Build with Confidence', 'DDD & Mounted Islands Architecture', 'Micro-Frontends Planned'];
    const island = DRN.React.mount('HelloReact', this, {
        title: titles[titleIndex]!,
        versions: versions ? JSON.parse(versions) : undefined,
        onReady: () => console.log('HelloReact component is ready'),
        onCardClick: () => {
            clickCount++;
            console.log(`Card clicked! Total clicks outside the component: ${clickCount}`);
        }
    });
    if (!island) return false;

    console.log('[DRN] HelloReact island mounted props:', island.getProps());
    const helloRoot = (this.shadowRoot ?? this).querySelector('.hello-react');
    const styles = helloRoot ? getComputedStyle(helloRoot) : null;
    const fadeDuration = Number.parseInt(styles?.getPropertyValue('--badge-fade-ms') || '400', 10);
    const cycleInterval = Number.parseInt(styles?.getPropertyValue('--title-cycle-ms') || '4000', 10);
    const interval = setInterval(() => {
        if (!island.getProps()) return;
        const badge = this.shadowRoot?.querySelector('.hello-react-badge');
        badge?.classList.add('is-transitioning');
        transitionTimer = setTimeout(() => {
            titleIndex = (titleIndex + 1) % titles.length;
            island.update({title: titles[titleIndex]!});
            badge?.classList.remove('is-transitioning');
        }, fadeDuration);
    }, cycleInterval);

    options.disposable = {
        dispose() {
            clearInterval(interval);
            clearTimeout(transitionTimer);
            island.dispose();
        }
    };
});
