import htmx from 'htmx.org';

//https://htmx.org/docs/#config
let config = htmx.config;
config.selfRequestsOnly = true;
config.allowEval = false;
config.refreshOnHistoryMiss = true;
config.historyCacheSize = 0;
config.timeout = 30_000;

document.addEventListener('htmx:beforeCleanupElement', event => {
    const element = event.detail?.elt;
    if (element instanceof Element)
        htmx.trigger(element, 'htmx:abort');
});

export { htmx };
