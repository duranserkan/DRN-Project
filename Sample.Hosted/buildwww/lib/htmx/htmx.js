import htmx from 'htmx.org';

//https://htmx.org/docs/#config
let config = htmx.config;
config.selfRequestsOnly = true;
config.allowEval = false;
config.refreshOnHistoryMiss = true;
config.historyCacheSize = 0;

export { htmx };